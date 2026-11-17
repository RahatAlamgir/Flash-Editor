using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>Average color around a pixel (transparent pixels count less), used by healing, blur and sharpen.</summary>
internal static class LocalMean
{
    /// <summary>
    /// The average of the square of pixels within <paramref name="radius"/> of (x, y), looking at every <paramref name="step"/>-th pixel.
    /// Colors are weighted by alpha so transparent pixels do not darken the result. False when nothing visible is near.
    /// </summary>
    public static bool At(PixelBuffer p, int x, int y, int radius, int step, out double r, out double g, out double b, out double a, Func<int, int, bool>? skip = null)
    {
        double sr = 0, sg = 0, sb = 0, sa = 0;
        int count = 0;
        var data = p.Data;
        for (int yy = y - radius; yy <= y + radius; yy += step)
        {
            if (yy < 0 || yy >= p.Height) continue;
            for (int xx = x - radius; xx <= x + radius; xx += step)
            {
                if (xx < 0 || xx >= p.Width || (skip != null && skip(xx, yy))) continue;
                int i = (yy * p.Width + xx) * 4;
                double alpha = data[i + 3];
                sb += data[i] * alpha; sg += data[i + 1] * alpha; sr += data[i + 2] * alpha;
                sa += alpha;
                count++;
            }
        }
        if (count == 0 || sa < 1) { r = g = b = a = 0; return false; }
        r = sr / sa; g = sg / sa; b = sb / sa; a = sa / count;
        return true;
    }
}
