using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>
/// Fast Gaussian blur approximated by three box blurs. Shared by Blur, Sharpen and
/// Background Removal (to soften mask edges).
/// </summary>
public static class Blur
{
    /// <summary>Blurs a color image. Uses premultiplied alpha so transparent areas don't create dark halos.</summary>
    public static PixelBuffer Gaussian(PixelBuffer source, double sigma, CancellationToken cancellation = default)
    {
        if (sigma < 0.3) return source.Clone();
        int w = source.Width, h = source.Height;

        var data = new float[w * h * 4];
        for (int i = 0; i < data.Length; i += 4)
        {
            float a = source.Data[i + 3] / 255f;
            data[i] = source.Data[i] * a;
            data[i + 1] = source.Data[i + 1] * a;
            data[i + 2] = source.Data[i + 2] * a;
            data[i + 3] = source.Data[i + 3];
        }

        GaussianInPlace(data, w, h, 4, sigma, cancellation);

        var result = new PixelBuffer(w, h);
        for (int i = 0; i < data.Length; i += 4)
        {
            float a = data[i + 3];
            if (a < 0.5f) continue;
            float inv = 255f / a;
            result.Data[i] = ColorMath.ClampByte(data[i] * inv);
            result.Data[i + 1] = ColorMath.ClampByte(data[i + 1] * inv);
            result.Data[i + 2] = ColorMath.ClampByte(data[i + 2] * inv);
            result.Data[i + 3] = ColorMath.ClampByte(a);
        }
        return result;
    }

    /// <summary>Blurs interleaved float data with any number of channels (e.g. 1 for a mask).</summary>
    public static void GaussianInPlace(float[] data, int width, int height, int channels, double sigma,
        CancellationToken cancellation = default)
    {
        if (sigma < 0.3) return;
        var temp = new float[data.Length];
        foreach (int radius in BoxRadiiForGaussian(sigma, 3))
        {
            BoxHorizontal(data, temp, width, height, channels, radius, cancellation);
            BoxVertical(temp, data, width, height, channels, radius, cancellation);
        }
    }

    /// <summary>Box sizes whose repeated application approximates a Gaussian of the given sigma.</summary>
    private static int[] BoxRadiiForGaussian(double sigma, int passes)
    {
        double ideal = Math.Sqrt(12 * sigma * sigma / passes + 1);
        int lower = (int)Math.Floor(ideal);
        if (lower % 2 == 0) lower--;
        int upper = lower + 2;
        double mIdeal = (12 * sigma * sigma - passes * lower * lower - 4 * passes * lower - 3 * passes) / (-4.0 * lower - 4);
        int m = (int)Math.Round(mIdeal);

        var radii = new int[passes];
        for (int i = 0; i < passes; i++)
            radii[i] = ((i < m ? lower : upper) - 1) / 2;
        return radii;
    }

    private static void BoxHorizontal(float[] src, float[] dst, int w, int h, int ch, int r, CancellationToken ct)
    {
        float scale = 1f / (2 * r + 1);
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            int row = y * w;
            for (int c = 0; c < ch; c++)
            {
                float sum = 0;
                for (int k = -r; k <= r; k++)
                    sum += src[(row + Math.Clamp(k, 0, w - 1)) * ch + c];
                for (int x = 0; x < w; x++)
                {
                    dst[(row + x) * ch + c] = sum * scale;
                    int add = Math.Min(w - 1, x + r + 1);
                    int remove = Math.Max(0, x - r);
                    sum += src[(row + add) * ch + c] - src[(row + remove) * ch + c];
                }
            }
        });
    }

    private static void BoxVertical(float[] src, float[] dst, int w, int h, int ch, int r, CancellationToken ct)
    {
        float scale = 1f / (2 * r + 1);
        Parallel.For(0, w, new ParallelOptions { CancellationToken = ct }, x =>
        {
            for (int c = 0; c < ch; c++)
            {
                float sum = 0;
                for (int k = -r; k <= r; k++)
                    sum += src[(Math.Clamp(k, 0, h - 1) * w + x) * ch + c];
                for (int y = 0; y < h; y++)
                {
                    dst[(y * w + x) * ch + c] = sum * scale;
                    int add = Math.Min(h - 1, y + r + 1);
                    int remove = Math.Max(0, y - r);
                    sum += src[(add * w + x) * ch + c] - src[(remove * w + x) * ch + c];
                }
            }
        });
    }
}
