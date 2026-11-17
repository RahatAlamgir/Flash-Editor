using FlashEditor.Core.Imaging;

namespace FlashEditor.AI.Inpainting;

/// <summary>
/// The model fills with plausible but smooth texture. This measures how much fine detail (leaves, grain, pebbles) the picture has around the
/// filled area and sharpens the fill so that it matches, never more than a moderate amount.
/// </summary>
public static class InpaintDetail
{
    /// <summary>Average strength of fine detail in the pixels selected by <paramref name="where"/> (higher = sharper / more texture).</summary>
    public static double DetailStrength(PixelBuffer picture, Func<int, int, bool> where, PixelRect region)
    {
        region = region.Intersect(picture.Bounds);
        double sum = 0;
        long count = 0;
        var d = picture.Data;
        int w = picture.Width, h = picture.Height;
        for (int y = Math.Max(1, region.Y); y < Math.Min(h - 1, region.Bottom); y += 2)
            for (int x = Math.Max(1, region.X); x < Math.Min(w - 1, region.Right); x += 2)
            {
                if (!where(x, y)) continue;
                sum += Math.Abs(4 * Luma(d, (y * w + x) * 4) - Luma(d, (y * w + x - 1) * 4) - Luma(d, (y * w + x + 1) * 4)
                                - Luma(d, ((y - 1) * w + x) * 4) - Luma(d, ((y + 1) * w + x) * 4));
                count++;
            }
        return count == 0 ? 0 : sum / count;
    }

    private static double Luma(byte[] d, int i) => 0.114 * d[i] + 0.587 * d[i + 1] + 0.299 * d[i + 2];

    /// <summary>How much to sharpen a fill whose detail strength is <paramref name="fill"/> to match surroundings with <paramref name="around"/> (0 = leave it).</summary>
    public static double AmountFor(double fill, double around)
    {
        if (fill <= 0.01 || around <= 0.01) return 0;
        double ratio = around / fill;
        return ratio < 1.15 ? 0 : Math.Min(1.8, (ratio - 1) * 0.9);
    }

    /// <summary>
    /// Sharpens the filled part (<paramref name="fill"/> has its pixels where alpha is set) with an unsharp mask. The neighbours that the blur sees come
    /// from <paramref name="original"/> where nothing was filled, so the edge of the fill does not get a dark ring.
    /// </summary>
    public static void Sharpen(PixelBuffer fill, PixelBuffer original, double amount)
    {
        if (amount <= 0.01) return;
        int w = fill.Width, h = fill.Height;
        var area = fill.VisibleBounds();
        if (area.IsEmpty) return;
        var region = area.Inflate(4).Intersect(fill.Bounds);

        // The picture as it looks with the fill on it.
        var mixed = original.Clone();
        for (int y = region.Y; y < region.Bottom; y++)
            for (int x = region.X; x < region.Right; x++)
            {
                int i = (y * w + x) * 4, a = fill.Data[i + 3];
                if (a == 0) continue;
                for (int c = 0; c < 3; c++) mixed.Data[i + c] = (byte)((fill.Data[i + c] * a + mixed.Data[i + c] * (255 - a)) / 255);
            }

        var blurred = Blur(mixed, region, 2);
        for (int y = region.Y; y < region.Bottom; y++)
            for (int x = region.X; x < region.Right; x++)
            {
                int i = (y * w + x) * 4;
                if (fill.Data[i + 3] == 0) continue;
                for (int c = 0; c < 3; c++)
                {
                    double m = mixed.Data[i + c];
                    fill.Data[i + c] = ColorMath.ClampByte(m + amount * (m - blurred.Data[i + c]));
                }
            }
    }

    // A soft blur (box blur twice) of the colors inside the region.
    private static PixelBuffer Blur(PixelBuffer source, PixelRect region, int radius)
    {
        int w = source.Width;
        var temp = source.Clone();
        var result = source.Clone();
        for (int y = region.Y; y < region.Bottom; y++)
            for (int x = region.X; x < region.Right; x++)
            {
                int from = Math.Max(0, x - radius), to = Math.Min(w - 1, x + radius), n = to - from + 1;
                for (int c = 0; c < 3; c++)
                {
                    int sum = 0;
                    for (int k = from; k <= to; k++) sum += source.Data[(y * w + k) * 4 + c];
                    temp.Data[(y * w + x) * 4 + c] = (byte)(sum / n);
                }
            }
        for (int y = region.Y; y < region.Bottom; y++)
            for (int x = region.X; x < region.Right; x++)
            {
                int from = Math.Max(0, y - radius), to = Math.Min(source.Height - 1, y + radius), n = to - from + 1;
                for (int c = 0; c < 3; c++)
                {
                    int sum = 0;
                    for (int k = from; k <= to; k++) sum += temp.Data[(k * w + x) * 4 + c];
                    result.Data[(y * w + x) * 4 + c] = (byte)(sum / n);
                }
            }
        return result;
    }
}
