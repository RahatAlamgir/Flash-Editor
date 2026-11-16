using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Transforms;

/// <summary>Geometric operations that produce a new pixel buffer: crop, flip, rotate.</summary>
public static class ImageTransforms
{
    /// <summary>Returns the part of <paramref name="source"/> inside <paramref name="rect"/>.</summary>
    public static PixelBuffer Crop(PixelBuffer source, PixelRect rect) => source.Crop(rect);

    public static PixelBuffer FlipHorizontal(PixelBuffer source)
    {
        var result = new PixelBuffer(source.Width, source.Height);
        int w = source.Width;
        Parallel.For(0, source.Height, y =>
        {
            for (int x = 0; x < w; x++)
                Buffer.BlockCopy(source.Data, source.IndexOf(x, y), result.Data, result.IndexOf(w - 1 - x, y), 4);
        });
        return result;
    }

    public static PixelBuffer FlipVertical(PixelBuffer source)
    {
        var result = new PixelBuffer(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
            Buffer.BlockCopy(source.Data, source.IndexOf(0, y), result.Data, result.IndexOf(0, source.Height - 1 - y), source.Stride);
        return result;
    }

    /// <summary>Rotates 90° clockwise (width and height swap).</summary>
    public static PixelBuffer Rotate90Clockwise(PixelBuffer source)
    {
        int w = source.Width, h = source.Height;
        var result = new PixelBuffer(h, w);
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
                Buffer.BlockCopy(source.Data, source.IndexOf(x, y), result.Data, result.IndexOf(h - 1 - y, x), 4);
        });
        return result;
    }

    /// <summary>Rotates 90° counter-clockwise (width and height swap).</summary>
    public static PixelBuffer Rotate90CounterClockwise(PixelBuffer source)
    {
        int w = source.Width, h = source.Height;
        var result = new PixelBuffer(h, w);
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
                Buffer.BlockCopy(source.Data, source.IndexOf(x, y), result.Data, result.IndexOf(y, w - 1 - x), 4);
        });
        return result;
    }

    public static PixelBuffer Rotate180(PixelBuffer source) => FlipVertical(FlipHorizontal(source));

    /// <summary>Size of the canvas needed to hold an image of the given size rotated by <paramref name="degrees"/>.</summary>
    public static (int Width, int Height) RotatedSize(int width, int height, double degrees)
    {
        double rad = degrees * Math.PI / 180;
        double cos = Math.Abs(Math.Cos(rad)), sin = Math.Abs(Math.Sin(rad));
        int newW = (int)Math.Ceiling(width * cos + height * sin - 1e-6);
        int newH = (int)Math.Ceiling(width * sin + height * cos - 1e-6);
        return (Math.Max(1, newW), Math.Max(1, newH));
    }

    /// <summary>
    /// Rotates by any angle (clockwise, in degrees). The canvas grows so nothing is cut off;
    /// new corners are transparent. Uses bilinear sampling for smooth edges.
    /// </summary>
    public static PixelBuffer Rotate(PixelBuffer source, double degrees)
    {
        double normalized = ((degrees % 360) + 360) % 360;
        if (normalized == 0) return source.Clone();
        if (normalized == 90) return Rotate90Clockwise(source);
        if (normalized == 180) return Rotate180(source);
        if (normalized == 270) return Rotate90CounterClockwise(source);

        var (newW, newH) = RotatedSize(source.Width, source.Height, degrees);
        var result = new PixelBuffer(newW, newH);
        double rad = degrees * Math.PI / 180;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);
        double scx = source.Width / 2.0, scy = source.Height / 2.0;
        double dcx = newW / 2.0, dcy = newH / 2.0;

        Parallel.For(0, newH, y =>
        {
            double dy = y + 0.5 - dcy;
            for (int x = 0; x < newW; x++)
            {
                double dx = x + 0.5 - dcx;
                // Inverse rotation: where in the source does this destination pixel come from?
                double sx = dx * cos + dy * sin + scx - 0.5;
                double sy = -dx * sin + dy * cos + scy - 0.5;
                Resampler.SampleBilinear(source, sx, sy, result.Data, result.IndexOf(x, y));
            }
        });
        return result;
    }
}
