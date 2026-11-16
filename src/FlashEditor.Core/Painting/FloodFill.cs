using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Painting;

/// <summary>Paint bucket: fills an area of similar color.</summary>
public static class FloodFill
{
    /// <summary>
    /// Builds a mask (one byte per pixel, 1 = matches) of pixels similar to the one at (x, y).
    /// </summary>
    /// <param name="tolerance">0 = exact color only, 255 = everything.</param>
    /// <param name="contiguous">True = only connected pixels; false = every matching pixel in the image.</param>
    public static byte[] BuildMask(PixelBuffer image, int x, int y, int tolerance, bool contiguous)
    {
        var mask = new byte[image.Width * image.Height];
        if (!image.Bounds.Contains(x, y)) return mask;

        var seed = image.GetPixel(x, y);
        var data = image.Data;
        int w = image.Width, h = image.Height;

        bool Matches(int px, int py)
        {
            int i = (py * w + px) * 4;
            // Fully transparent pixels count as one color regardless of their hidden RGB.
            if (seed.A == 0 && data[i + 3] == 0) return true;
            int diff = Math.Max(Math.Max(Math.Abs(data[i + 2] - seed.R), Math.Abs(data[i + 1] - seed.G)),
                                Math.Max(Math.Abs(data[i] - seed.B), Math.Abs(data[i + 3] - seed.A)));
            return diff <= tolerance;
        }

        if (!contiguous)
        {
            Parallel.For(0, h, py =>
            {
                for (int px = 0; px < w; px++)
                    if (Matches(px, py)) mask[py * w + px] = 1;
            });
            return mask;
        }

        // Scanline flood fill: fill whole horizontal runs, then queue the rows above and below.
        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (sx, sy) = stack.Pop();
            if (mask[sy * w + sx] != 0 || !Matches(sx, sy)) continue;

            int left = sx, right = sx;
            while (left > 0 && mask[sy * w + left - 1] == 0 && Matches(left - 1, sy)) left--;
            while (right < w - 1 && mask[sy * w + right + 1] == 0 && Matches(right + 1, sy)) right++;

            for (int px = left; px <= right; px++)
            {
                mask[sy * w + px] = 1;
                if (sy > 0 && mask[(sy - 1) * w + px] == 0 && Matches(px, sy - 1)) stack.Push((px, sy - 1));
                if (sy < h - 1 && mask[(sy + 1) * w + px] == 0 && Matches(px, sy + 1)) stack.Push((px, sy + 1));
            }
        }
        return mask;
    }

    /// <summary>Fills the matching area with <paramref name="color"/>. Returns the changed rectangle.</summary>
    public static PixelRect Fill(PixelBuffer image, int x, int y, ColorRgba color, int tolerance,
        bool contiguous, Selection? selection)
    {
        if (selection != null && !selection.IsSelected(x, y) && contiguous) return PixelRect.Empty;

        var mask = BuildMask(image, x, y, tolerance, contiguous);
        int w = image.Width;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        double colorAlpha = color.A / 255.0;

        for (int py = 0; py < image.Height; py++)
        {
            for (int px = 0; px < w; px++)
            {
                if (mask[py * w + px] == 0) continue;
                double strength = selection?.Coverage(px, py) ?? 1.0;
                if (strength <= 0) continue;

                int i = (py * w + px) * 4;
                if (strength >= 1 && color.A == 255)
                {
                    image.Data[i] = color.B; image.Data[i + 1] = color.G; image.Data[i + 2] = color.R; image.Data[i + 3] = 255;
                }
                else
                {
                    BlendMath.Over(image.Data, i, color.B, color.G, color.R, colorAlpha * strength);
                }
                minX = Math.Min(minX, px); maxX = Math.Max(maxX, px);
                minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
            }
        }
        return maxX < 0 ? PixelRect.Empty : new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
