using System.Numerics;
using System.Runtime.InteropServices;
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
        var src = source.Data;

        var data = new float[w * h * 4];
        Parallel.For(0, h, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            for (int i = y * w * 4, end = i + w * 4; i < end; i += 4)
            {
                float a = src[i + 3] / 255f;
                data[i] = src[i] * a;
                data[i + 1] = src[i + 1] * a;
                data[i + 2] = src[i + 2] * a;
                data[i + 3] = src[i + 3];
            }
        });

        GaussianInPlace(data, w, h, 4, sigma, cancellation);

        var result = new PixelBuffer(w, h);
        var dst = result.Data;
        Parallel.For(0, h, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            for (int i = y * w * 4, end = i + w * 4; i < end; i += 4)
            {
                float a = data[i + 3];
                if (a < 0.5f) continue;
                float inv = 255f / a;
                dst[i] = ColorMath.ClampByte(data[i] * inv);
                dst[i + 1] = ColorMath.ClampByte(data[i + 1] * inv);
                dst[i + 2] = ColorMath.ClampByte(data[i + 2] * inv);
                dst[i + 3] = ColorMath.ClampByte(a);
            }
        });
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

    /// <summary>Blurs every row with a running sum. All channels of a pixel are handled together, so memory is read in order.</summary>
    private static void BoxHorizontal(float[] src, float[] dst, int w, int h, int ch, int r, CancellationToken ct)
    {
        float scale = 1f / (2 * r + 1);
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, () => new float[ch], (y, _, sum) =>
        {
            int row = y * w * ch;
            Array.Clear(sum);
            for (int k = -r; k <= r; k++)
            {
                int p = row + Math.Clamp(k, 0, w - 1) * ch;
                for (int c = 0; c < ch; c++) sum[c] += src[p + c];
            }
            for (int x = 0; x < w; x++)
            {
                int o = row + x * ch;
                int add = row + Math.Min(w - 1, x + r + 1) * ch;
                int remove = row + Math.Max(0, x - r) * ch;
                for (int c = 0; c < ch; c++)
                {
                    dst[o + c] = sum[c] * scale;
                    sum[c] += src[add + c] - src[remove + c];
                }
            }
            return sum;
        }, _ => { });
    }

    /// <summary>
    /// Blurs the columns. Instead of walking down one column at a time (every step jumps a whole row ahead in memory,
    /// which is very slow on big pictures), a band of columns is processed row by row with one running sum per
    /// column, so every read and write is a short run of neighboring values.
    /// </summary>
    private static void BoxVertical(float[] src, float[] dst, int w, int h, int ch, int r, CancellationToken ct)
    {
        float scale = 1f / (2 * r + 1);
        int rowLength = w * ch;
        int bandLength = Math.Min(rowLength, 128 * ch);          // floats per band: 128 pixels wide
        int bands = (rowLength + bandLength - 1) / bandLength;

        Parallel.For(0, bands, new ParallelOptions { CancellationToken = ct }, () => new float[bandLength], (band, _, sum) =>
        {
            int start = band * bandLength;
            int length = Math.Min(bandLength, rowLength - start);
            var sums = sum.AsSpan(0, length);
            sums.Clear();

            for (int k = -r; k <= r; k++)
                Add(sums, src.AsSpan(Math.Clamp(k, 0, h - 1) * rowLength + start, length));

            for (int y = 0; y < h; y++)
            {
                Scale(sums, scale, dst.AsSpan(y * rowLength + start, length));
                int add = Math.Min(h - 1, y + r + 1), remove = Math.Max(0, y - r);
                AddSubtract(sums, src.AsSpan(add * rowLength + start, length), src.AsSpan(remove * rowLength + start, length));
            }
            return sum;
        }, _ => { });
    }

    // Small vector helpers: the same arithmetic as a plain loop, several numbers at a time.

    private static void Add(Span<float> sums, ReadOnlySpan<float> row)
    {
        int i = 0, n = Vector<float>.Count;
        var s = MemoryMarshal.Cast<float, Vector<float>>(sums);
        var r = MemoryMarshal.Cast<float, Vector<float>>(row);
        for (int v = 0; v < s.Length; v++) s[v] += r[v];
        for (i = s.Length * n; i < sums.Length; i++) sums[i] += row[i];
    }

    private static void AddSubtract(Span<float> sums, ReadOnlySpan<float> add, ReadOnlySpan<float> remove)
    {
        int n = Vector<float>.Count;
        var s = MemoryMarshal.Cast<float, Vector<float>>(sums);
        var a = MemoryMarshal.Cast<float, Vector<float>>(add);
        var d = MemoryMarshal.Cast<float, Vector<float>>(remove);
        for (int v = 0; v < s.Length; v++) s[v] += a[v] - d[v];
        for (int i = s.Length * n; i < sums.Length; i++) sums[i] += add[i] - remove[i];
    }

    private static void Scale(ReadOnlySpan<float> sums, float scale, Span<float> output)
    {
        int n = Vector<float>.Count;
        var s = MemoryMarshal.Cast<float, Vector<float>>(sums);
        var o = MemoryMarshal.Cast<float, Vector<float>>(output);
        var factor = new Vector<float>(scale);
        for (int v = 0; v < s.Length; v++) o[v] = s[v] * factor;
        for (int i = s.Length * n; i < sums.Length; i++) output[i] = sums[i] * scale;
    }
}
