using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Painting;

/// <summary>
/// One brush or eraser stroke, from mouse-down to mouse-up.
/// <para>
/// Dabs are accumulated into a coverage mask (taking the maximum, not adding), and the result is always
/// recomputed from the untouched starting pixels. That way overlapping dabs inside one stroke never
/// build up darker spots, and a 50%-opacity stroke stays exactly 50%.
/// </para>
/// </summary>
public sealed class BrushStroke
{
    private readonly PixelBuffer _target;
    private readonly BrushSettings _brush;
    private readonly ColorRgba _color;
    private readonly bool _erase;
    private readonly Selection? _selection;
    private readonly PixelBuffer? _restoreFrom;
    private readonly float[] _coverage;
    private PointD? _last;
    private double _carry; // distance left over from the previous segment, keeps dab spacing even

    public BrushStroke(PixelBuffer target, BrushSettings brush, ColorRgba color, bool erase, Selection? selection)
    {
        _target = target;
        _brush = brush;
        _color = color;
        _erase = erase;
        _selection = selection;
        _coverage = new float[target.Width * target.Height];
        Original = target.Clone();
    }

    /// <summary>
    /// A "restore" stroke: paints pixels back from <paramref name="restoreFrom"/> (same size as the target),
    /// e.g. the picture as it was before its background was removed.
    /// </summary>
    public BrushStroke(PixelBuffer target, BrushSettings brush, PixelBuffer restoreFrom, Selection? selection)
        : this(target, brush, ColorRgba.Transparent, erase: false, selection)
    {
        if (restoreFrom.Width != target.Width || restoreFrom.Height != target.Height)
            throw new ArgumentException("The restore source must be the same size as the layer.");
        _restoreFrom = restoreFrom;
    }

    /// <summary>Copy of the pixels before the stroke started (needed for undo).</summary>
    public PixelBuffer Original { get; }

    /// <summary>Everything the stroke has touched so far.</summary>
    public PixelRect TotalDirty { get; private set; } = PixelRect.Empty;

    /// <summary>Adds the next mouse position. Returns the rectangle that changed.</summary>
    public PixelRect AddPoint(PointD point)
    {
        PixelRect dirty = PixelRect.Empty;
        double spacing = Math.Max(1.0, _brush.Size * 0.12);

        if (_last is not PointD last)
        {
            dirty = Stamp(point);
            _carry = 0;
        }
        else
        {
            double distance = last.DistanceTo(point);
            double travelled = spacing - _carry;
            while (travelled <= distance)
            {
                double t = travelled / distance;
                dirty = dirty.Union(Stamp(new PointD(last.X + (point.X - last.X) * t, last.Y + (point.Y - last.Y) * t)));
                travelled += spacing;
            }
            _carry = distance - (travelled - spacing);
        }

        _last = point;
        if (!dirty.IsEmpty)
        {
            Composite(dirty);
            TotalDirty = TotalDirty.Union(dirty);
        }
        return dirty;
    }

    private readonly Random _random = new();

    // The flat tip of the calligraphy brush: slanted 45 degrees, a quarter as thick as it is long.
    private const double ChiselFlatness = 0.25;
    private static readonly double ChiselCos = Math.Cos(Math.PI / 4), ChiselSin = Math.Sin(Math.PI / 4);

    /// <summary>Adds one dab of the current brush type to the coverage mask.</summary>
    private PixelRect Stamp(PointD center)
    {
        double radius = _brush.Radius;
        var type = _brush.Type;
        if (type is BrushType.Spray or BrushType.Splatter) return StampScatter(center, radius, type);

        // How far from the center a dab can reach (a square sticks out into its corners).
        double reach = type == BrushType.Square ? radius * 1.45 : radius;
        var rect = PixelRect.FromBounds(center.X - reach - 1, center.Y - reach - 1, center.X + reach + 1, center.Y + reach + 1)
                            .Intersect(_target.Bounds);
        if (rect.IsEmpty) return rect;

        double hardness = type == BrushType.Pencil ? 1.0 : Math.Clamp(_brush.Hardness, 0, 1);
        double inner = radius * hardness;
        double falloff = Math.Max(radius - inner, 0.0001);
        int w = _target.Width;

        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            double dy = y + 0.5 - center.Y;
            for (int x = rect.X; x < rect.Right; x++)
            {
                double dx = x + 0.5 - center.X;
                // "Distance" from the center in the shape of the tip: a circle, a square or a slanted flat ellipse.
                double d = type switch
                {
                    BrushType.Square => Math.Max(Math.Abs(dx), Math.Abs(dy)),
                    BrushType.Calligraphy => Chisel(dx, dy),
                    _ => Math.Sqrt(dx * dx + dy * dy),
                };
                if (d >= radius + 0.5) continue;

                // Smooth falloff between inner (hard core) and radius, plus 1px anti-aliased rim.
                double soft = d <= inner ? 1.0 : 1.0 - SmoothStep((d - inner) / falloff);
                double rim = Math.Clamp(radius + 0.5 - d, 0, 1);
                double c = soft * rim;

                // Texture: the grain depends on the pixel (not on the dab), so overlapping dabs never change it.
                if (type == BrushType.Pencil) c *= 0.62 + 0.38 * Grain(x, y);
                else if (type == BrushType.Chalk) c *= Grain(x, y) > 0.38 ? 0.55 + 0.45 * Grain(y, x) : 0.0;

                int i = y * w + x;
                if (c > _coverage[i]) _coverage[i] = (float)c;
            }
        }
        return rect;
    }

    /// <summary>Distance in the slanted flat tip's own shape (it is a thin ellipse turned 45 degrees).</summary>
    private static double Chisel(double dx, double dy)
    {
        double along = dx * ChiselCos + dy * ChiselSin;     // the long way
        double across = -dx * ChiselSin + dy * ChiselCos;   // the thin way
        return Math.Sqrt(along * along + across * across / (ChiselFlatness * ChiselFlatness));
    }

    /// <summary>Spray (a mist of small dots, denser in the middle) and Splatter (a few blobs of different sizes).</summary>
    private PixelRect StampScatter(PointD center, double radius, BrushType type)
    {
        double reach = type == BrushType.Splatter ? radius * 1.6 : radius;
        var rect = PixelRect.FromBounds(center.X - reach - 2, center.Y - reach - 2, center.X + reach + 2, center.Y + reach + 2)
                            .Intersect(_target.Bounds);
        if (rect.IsEmpty) return rect;

        if (type == BrushType.Spray)
        {
            int dots = Math.Clamp((int)(radius * 2.2), 6, 220);
            double dotRadius = radius > 60 ? 1.2 : 0.7;
            for (int k = 0; k < dots; k++)
            {
                double angle = _random.NextDouble() * 2 * Math.PI;
                double distance = radius * Math.Sqrt(_random.NextDouble());
                double strength = 0.45 + 0.55 * (1 - distance / radius);   // fainter towards the edge
                Blob(center.X + Math.Cos(angle) * distance, center.Y + Math.Sin(angle) * distance, dotRadius, strength);
            }
        }
        else
        {
            int blobs = Math.Max(2, (int)(radius * 0.18));
            for (int k = 0; k < blobs; k++)
            {
                double angle = _random.NextDouble() * 2 * Math.PI;
                double distance = reach * Math.Sqrt(_random.NextDouble());
                double size = Math.Max(0.8, radius * (0.05 + 0.3 * Math.Pow(_random.NextDouble(), 3)));  // mostly small, a few big
                Blob(center.X + Math.Cos(angle) * distance, center.Y + Math.Sin(angle) * distance, size, 1.0);
            }
        }
        return rect;
    }

    /// <summary>A small round dot with a soft edge, added to the coverage mask.</summary>
    private void Blob(double cx, double cy, double radius, double strength)
    {
        var area = PixelRect.FromBounds(cx - radius - 1, cy - radius - 1, cx + radius + 1, cy + radius + 1).Intersect(_target.Bounds);
        for (int y = area.Y; y < area.Bottom; y++)
            for (int x = area.X; x < area.Right; x++)
            {
                double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                double c = Math.Clamp(radius + 0.5 - d, 0, 1) * strength;
                int i = y * _target.Width + x;
                if (c > _coverage[i]) _coverage[i] = (float)c;
            }
    }

    /// <summary>Repeatable pseudo-random number 0..1 for a pixel position (same pixel, same value, every time).</summary>
    private static double Grain(int x, int y)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535.0;
        }
    }

    /// <summary>Recomputes target pixels inside <paramref name="rect"/> from the original pixels.</summary>
    private void Composite(PixelRect rect)
    {
        var dst = _target.Data;
        var src = Original.Data;
        int w = _target.Width;
        double colorAlpha = _color.A / 255.0;

        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            for (int x = rect.X; x < rect.Right; x++)
            {
                double strength = _coverage[y * w + x] * _brush.Opacity;
                if (_selection != null) strength *= _selection.Coverage(x, y);

                int i = (y * w + x) * 4;
                dst[i] = src[i]; dst[i + 1] = src[i + 1]; dst[i + 2] = src[i + 2]; dst[i + 3] = src[i + 3];
                if (strength <= 0) continue;

                if (_restoreFrom != null)
                    BlendMath.Lerp(dst, i, src, i, _restoreFrom.Data, i, strength);
                else if (_erase)
                    dst[i + 3] = ColorMath.ClampByte(src[i + 3] * (1 - strength));
                else
                    BlendMath.Over(dst, i, _color.B, _color.G, _color.R, strength * colorAlpha);
            }
        }
    }

    private static double SmoothStep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }
}
