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

    /// <summary>Adds one round dab to the coverage mask.</summary>
    private PixelRect Stamp(PointD center)
    {
        double radius = _brush.Radius;
        var rect = PixelRect.FromBounds(center.X - radius - 1, center.Y - radius - 1,
                                        center.X + radius + 1, center.Y + radius + 1)
                            .Intersect(_target.Bounds);
        if (rect.IsEmpty) return rect;

        double inner = radius * Math.Clamp(_brush.Hardness, 0, 1);
        double falloff = Math.Max(radius - inner, 0.0001);
        int w = _target.Width;

        for (int y = rect.Y; y < rect.Bottom; y++)
        {
            double dy = y + 0.5 - center.Y;
            for (int x = rect.X; x < rect.Right; x++)
            {
                double dx = x + 0.5 - center.X;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d >= radius + 0.5) continue;

                // Smooth falloff between inner (hard core) and radius, plus 1px anti-aliased rim.
                double soft = d <= inner ? 1.0 : 1.0 - SmoothStep((d - inner) / falloff);
                double rim = Math.Clamp(radius + 0.5 - d, 0, 1);
                float c = (float)(soft * rim);

                int i = y * w + x;
                if (c > _coverage[i]) _coverage[i] = c;
            }
        }
        return rect;
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

                if (_erase)
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
