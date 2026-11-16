using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Selections;

/// <summary>
/// A selected area of the canvas. Holds a per-pixel mask (0 = not selected, 255 = selected)
/// plus outline polygons the UI draws as "marching ants". Selections are immutable.
/// </summary>
public sealed class Selection
{
    public int CanvasWidth { get; }
    public int CanvasHeight { get; }

    /// <summary>One byte per canvas pixel, row by row.</summary>
    public byte[] Mask { get; }

    /// <summary>Smallest rectangle containing every selected pixel.</summary>
    public PixelRect Bounds { get; }

    /// <summary>Outline polygons in image coordinates (even-odd).</summary>
    public IReadOnlyList<IReadOnlyList<PointD>> Outline { get; }

    public bool IsEmpty => Bounds.IsEmpty;

    private Selection(int width, int height, byte[] mask, IReadOnlyList<IReadOnlyList<PointD>> outline)
    {
        CanvasWidth = width;
        CanvasHeight = height;
        Mask = mask;
        Outline = outline;
        Bounds = ComputeBounds(mask, width, height);
    }

    /// <summary>Selection amount at a pixel, 0..1. Pixels outside the canvas are unselected.</summary>
    public double Coverage(int x, int y)
    {
        if (x < 0 || y < 0 || x >= CanvasWidth || y >= CanvasHeight) return 0;
        return Mask[y * CanvasWidth + x] / 255.0;
    }

    public bool IsSelected(int x, int y) => Coverage(x, y) > 0;

    public static Selection? FromRectangle(PixelRect rect, int canvasWidth, int canvasHeight)
    {
        rect = rect.Intersect(new PixelRect(0, 0, canvasWidth, canvasHeight));
        if (rect.IsEmpty) return null;

        var mask = new byte[canvasWidth * canvasHeight];
        for (int y = rect.Y; y < rect.Bottom; y++)
            Array.Fill(mask, (byte)255, y * canvasWidth + rect.X, rect.Width);

        var outline = new[] { RectanglePolygon(rect) };
        return Create(canvasWidth, canvasHeight, mask, outline);
    }

    public static Selection? FromPolygon(IReadOnlyList<PointD> points, int canvasWidth, int canvasHeight)
    {
        if (points.Count < 3) return null;
        var mask = new byte[canvasWidth * canvasHeight];
        PolygonRasterizer.Fill(mask, canvasWidth, canvasHeight, new[] { points });
        return Create(canvasWidth, canvasHeight, mask, new[] { points.ToArray() });
    }

    /// <summary>A selection from a mask (0..255 per pixel, soft values allowed). The mask is not copied.</summary>
    public static Selection? FromMask(byte[] mask, int canvasWidth, int canvasHeight)
    {
        if (mask.Length != canvasWidth * canvasHeight) throw new ArgumentException("Mask size does not match the canvas.");
        return Create(canvasWidth, canvasHeight, mask, MaskOutline.Trace(mask, canvasWidth, canvasHeight));
    }

    /// <summary>An ellipse (or circle) fitting <paramref name="rect"/>.</summary>
    public static Selection? FromEllipse(PixelRect rect, int canvasWidth, int canvasHeight)
    {
        if (rect.Width < 1 || rect.Height < 1) return null;
        double cx = rect.X + rect.Width / 2.0, cy = rect.Y + rect.Height / 2.0;
        double rx = rect.Width / 2.0, ry = rect.Height / 2.0;
        int steps = (int)Math.Clamp((rx + ry) * 1.5, 24, 720);
        var points = new PointD[steps];
        for (int i = 0; i < steps; i++)
        {
            double a = 2 * Math.PI * i / steps;
            points[i] = new PointD(cx + rx * Math.Cos(a), cy + ry * Math.Sin(a));
        }
        return FromPolygon(points, canvasWidth, canvasHeight);
    }

    /// <summary>How a new selection is combined with an existing one (Shift = add, Alt = subtract).</summary>
    public enum CombineMode { Replace, Add, Subtract }

    /// <summary>Combines two selections. Null means "nothing selected".</summary>
    public static Selection? Combine(Selection? existing, Selection? added, CombineMode mode)
    {
        if (mode == CombineMode.Replace || existing == null)
            return mode == CombineMode.Subtract ? null : added;
        if (added == null) return existing;

        var mask = (byte[])existing.Mask.Clone();
        for (int i = 0; i < mask.Length; i++)
        {
            mask[i] = mode == CombineMode.Add
                ? Math.Max(mask[i], added.Mask[i])
                : (byte)(mask[i] * (255 - added.Mask[i]) / 255);
        }
        return FromMask(mask, existing.CanvasWidth, existing.CanvasHeight);
    }

    public static Selection All(int canvasWidth, int canvasHeight)
        => FromRectangle(new PixelRect(0, 0, canvasWidth, canvasHeight), canvasWidth, canvasHeight)!;

    /// <summary>Selects everything that was not selected, and vice versa.</summary>
    public Selection? Invert()
    {
        var mask = new byte[Mask.Length];
        for (int i = 0; i < mask.Length; i++) mask[i] = (byte)(255 - Mask[i]);

        // Canvas border + old outline drawn with even-odd = the inverted area.
        var outline = new List<IReadOnlyList<PointD>>
        {
            RectanglePolygon(new PixelRect(0, 0, CanvasWidth, CanvasHeight)),
        };
        outline.AddRange(Outline);
        return Create(CanvasWidth, CanvasHeight, mask, outline);
    }

    /// <summary>Moves the selection by whole pixels. Parts moved off the canvas are dropped.</summary>
    public Selection? Offset(int dx, int dy)
    {
        var mask = new byte[Mask.Length];
        var b = Bounds;
        for (int y = b.Y; y < b.Bottom; y++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= CanvasHeight) continue;
            for (int x = b.X; x < b.Right; x++)
            {
                int nx = x + dx;
                if (nx < 0 || nx >= CanvasWidth) continue;
                mask[ny * CanvasWidth + nx] = Mask[y * CanvasWidth + x];
            }
        }
        var outline = Outline
            .Select(poly => (IReadOnlyList<PointD>)poly.Select(p => p.Offset(dx, dy)).ToArray())
            .ToArray();
        return Create(CanvasWidth, CanvasHeight, mask, outline);
    }

    private static Selection? Create(int w, int h, byte[] mask, IReadOnlyList<IReadOnlyList<PointD>> outline)
    {
        var selection = new Selection(w, h, mask, outline);
        return selection.IsEmpty ? null : selection;
    }

    private static PointD[] RectanglePolygon(PixelRect r) => new[]
    {
        new PointD(r.X, r.Y), new PointD(r.Right, r.Y),
        new PointD(r.Right, r.Bottom), new PointD(r.X, r.Bottom),
    };

    private static PixelRect ComputeBounds(byte[] mask, int width, int height)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            int first = Array.FindIndex(mask, row, width, v => v != 0);
            if (first < 0) continue;
            int last = Array.FindLastIndex(mask, row + width - 1, width, v => v != 0);
            minY = Math.Min(minY, y);
            maxY = y;
            minX = Math.Min(minX, first - row);
            maxX = Math.Max(maxX, last - row);
        }
        return maxX < 0 ? PixelRect.Empty : new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
