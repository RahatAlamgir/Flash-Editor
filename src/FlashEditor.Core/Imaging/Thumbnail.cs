namespace FlashEditor.Core.Imaging;

/// <summary>Makes small preview pictures (the little icon next to each layer).</summary>
public static class Thumbnail
{
    /// <summary>
    /// Size of a thumbnail of a <paramref name="width"/> x <paramref name="height"/> picture that fits in
    /// <paramref name="maxWidth"/> x <paramref name="maxHeight"/> (never larger than the picture itself).
    /// </summary>
    public static (int Width, int Height) FitSize(int width, int height, int maxWidth, int maxHeight)
    {
        double scale = Math.Min(1.0, Math.Min(maxWidth / (double)width, maxHeight / (double)height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>
    /// A shrunken copy of <paramref name="source"/> that fits in <paramref name="maxWidth"/> x <paramref name="maxHeight"/>.
    /// Each thumbnail pixel averages a small grid of source pixels (alpha-weighted, so transparent edges do not
    /// darken the colors). The cost depends on the thumbnail size, not on the picture size.
    /// </summary>
    public static PixelBuffer Create(PixelBuffer source, int maxWidth, int maxHeight)
    {
        var (tw, th) = FitSize(source.Width, source.Height, maxWidth, maxHeight);
        var result = new PixelBuffer(tw, th);
        Fill(source, result);
        return result;
    }

    /// <summary>Redraws <paramref name="target"/> (the thumbnail) from <paramref name="source"/>, keeping the target's size.</summary>
    public static void Fill(PixelBuffer source, PixelBuffer target)
    {
        const int Samples = 4; // 4 x 4 source pixels per thumbnail pixel (fewer when the cell is smaller)
        int sw = source.Width, sh = source.Height, tw = target.Width, th = target.Height;
        var src = source.Data;
        var dst = target.Data;

        for (int ty = 0; ty < th; ty++)
        {
            int y0 = (int)((long)ty * sh / th), cellH = Math.Max(1, (int)((long)(ty + 1) * sh / th) - y0);
            int ny = Math.Min(cellH, Samples);
            for (int tx = 0; tx < tw; tx++)
            {
                int x0 = (int)((long)tx * sw / tw), cellW = Math.Max(1, (int)((long)(tx + 1) * sw / tw) - x0);
                int nx = Math.Min(cellW, Samples);

                long a = 0, b = 0, g = 0, r = 0;
                int count = 0;
                for (int sy = 0; sy < ny; sy++)
                {
                    // Evenly spaced sample positions inside the cell (the middle of each of n equal slices).
                    int y = Math.Min(sh - 1, y0 + (int)((sy + 0.5) * cellH / ny));
                    int row = y * sw * 4;
                    for (int sx = 0; sx < nx; sx++)
                    {
                        int x = Math.Min(sw - 1, x0 + (int)((sx + 0.5) * cellW / nx));
                        int i = row + x * 4;
                        int alpha = src[i + 3];
                        a += alpha;
                        b += src[i] * alpha;
                        g += src[i + 1] * alpha;
                        r += src[i + 2] * alpha;
                        count++;
                    }
                }

                int o = (ty * tw + tx) * 4;
                if (a == 0 || count == 0)
                {
                    dst[o] = dst[o + 1] = dst[o + 2] = dst[o + 3] = 0;
                    continue;
                }
                dst[o] = (byte)(b / a);
                dst[o + 1] = (byte)(g / a);
                dst[o + 2] = (byte)(r / a);
                dst[o + 3] = (byte)(a / count);
            }
        }
    }
}
