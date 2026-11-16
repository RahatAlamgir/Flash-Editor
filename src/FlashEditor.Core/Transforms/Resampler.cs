using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Transforms;

public enum ResampleMode
{
    /// <summary>Smooth, good for photos (bilinear when enlarging, area-averaging when shrinking).</summary>
    Smooth,
    /// <summary>Hard pixel edges, good for pixel art.</summary>
    NearestNeighbor,
    /// <summary>Sharper than Smooth (bicubic). Good all-round choice.</summary>
    Bicubic,
    /// <summary>Sharpest (Lanczos-3). Best for enlarging photos; keeps fine detail when shrinking.</summary>
    Lanczos,
}

/// <summary>Image scaling and sub-pixel sampling.</summary>
public static class Resampler
{
    public static PixelBuffer Resize(PixelBuffer source, int width, int height, ResampleMode mode = ResampleMode.Smooth)
    {
        if (width == source.Width && height == source.Height) return source.Clone();
        return mode == ResampleMode.NearestNeighbor
            ? ResizeNearest(source, width, height)
            : ResizeFiltered(source, width, height, mode);
    }

    /// <summary>Filter kernel and its radius (in source pixels at scale 1).</summary>
    private static (Func<double, double> Kernel, double Radius) GetFilter(ResampleMode mode) => mode switch
    {
        ResampleMode.Bicubic => (Cubic, 2),
        ResampleMode.Lanczos => (Lanczos3, 3),
        _ => (x => Math.Max(0, 1 - Math.Abs(x)), 1),
    };

    /// <summary>Catmull-Rom style cubic (a = -0.5).</summary>
    private static double Cubic(double x)
    {
        const double a = -0.5;
        x = Math.Abs(x);
        if (x < 1) return (a + 2) * x * x * x - (a + 3) * x * x + 1;
        if (x < 2) return a * x * x * x - 5 * a * x * x + 8 * a * x - 4 * a;
        return 0;
    }

    private static double Lanczos3(double x)
    {
        x = Math.Abs(x);
        if (x < 1e-8) return 1;
        if (x >= 3) return 0;
        double px = Math.PI * x;
        return 3 * Math.Sin(px) * Math.Sin(px / 3) / (px * px);
    }

    /// <summary>
    /// Writes the bilinearly-interpolated color at fractional source position (sx, sy) into dst[di].
    /// Positions outside the image fade to transparent.
    /// </summary>
    public static void SampleBilinear(PixelBuffer source, double sx, double sy, byte[] dst, int di)
    {
        int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
        double fx = sx - x0, fy = sy - y0;
        double b = 0, g = 0, r = 0, a = 0;

        void Add(int x, int y, double weight)
        {
            if (weight <= 0 || x < 0 || y < 0 || x >= source.Width || y >= source.Height) return;
            int i = source.IndexOf(x, y);
            double pa = source.Data[i + 3] * weight;
            b += source.Data[i] * pa;
            g += source.Data[i + 1] * pa;
            r += source.Data[i + 2] * pa;
            a += pa;
        }

        Add(x0, y0, (1 - fx) * (1 - fy));
        Add(x0 + 1, y0, fx * (1 - fy));
        Add(x0, y0 + 1, (1 - fx) * fy);
        Add(x0 + 1, y0 + 1, fx * fy);

        if (a <= 0.001)
        {
            dst[di] = dst[di + 1] = dst[di + 2] = dst[di + 3] = 0;
            return;
        }
        dst[di] = ColorMath.ClampByte(b / a);
        dst[di + 1] = ColorMath.ClampByte(g / a);
        dst[di + 2] = ColorMath.ClampByte(r / a);
        dst[di + 3] = ColorMath.ClampByte(a);
    }

    private static PixelBuffer ResizeNearest(PixelBuffer source, int width, int height)
    {
        var result = new PixelBuffer(width, height);
        double sxScale = (double)source.Width / width, syScale = (double)source.Height / height;
        Parallel.For(0, height, y =>
        {
            int sy = Math.Min(source.Height - 1, (int)((y + 0.5) * syScale));
            for (int x = 0; x < width; x++)
            {
                int sx = Math.Min(source.Width - 1, (int)((x + 0.5) * sxScale));
                Buffer.BlockCopy(source.Data, source.IndexOf(sx, sy), result.Data, result.IndexOf(x, y), 4);
            }
        });
        return result;
    }

    /// <summary>Separable resize with a filter that widens when shrinking (no aliasing).</summary>
    private static PixelBuffer ResizeFiltered(PixelBuffer source, int width, int height, ResampleMode mode)
    {
        var filter = GetFilter(mode);
        int sw = source.Width, sh = source.Height;

        // Premultiply so transparent pixels don't bleed their hidden color.
        var pre = new float[sw * sh * 4];
        Parallel.For(0, sh, y =>
        {
            for (int x = 0; x < sw; x++)
            {
                int i = (y * sw + x) * 4;
                float a = source.Data[i + 3] / 255f;
                pre[i] = source.Data[i] * a;
                pre[i + 1] = source.Data[i + 1] * a;
                pre[i + 2] = source.Data[i + 2] * a;
                pre[i + 3] = source.Data[i + 3];
            }
        });

        var xWeights = BuildWeights(sw, width, filter);
        var yWeights = BuildWeights(sh, height, filter);

        // Horizontal pass: sw x sh -> width x sh
        var tmp = new float[width * sh * 4];
        Parallel.For(0, sh, y =>
        {
            for (int x = 0; x < width; x++)
            {
                var (start, weights) = xWeights[x];
                float b = 0, g = 0, r = 0, a = 0;
                for (int k = 0; k < weights.Length; k++)
                {
                    int si = (y * sw + start + k) * 4;
                    float wgt = weights[k];
                    b += pre[si] * wgt; g += pre[si + 1] * wgt; r += pre[si + 2] * wgt; a += pre[si + 3] * wgt;
                }
                int ti = (y * width + x) * 4;
                tmp[ti] = b; tmp[ti + 1] = g; tmp[ti + 2] = r; tmp[ti + 3] = a;
            }
        });

        // Vertical pass: width x sh -> width x height, then un-premultiply.
        var result = new PixelBuffer(width, height);
        Parallel.For(0, height, y =>
        {
            var (start, weights) = yWeights[y];
            for (int x = 0; x < width; x++)
            {
                float b = 0, g = 0, r = 0, a = 0;
                for (int k = 0; k < weights.Length; k++)
                {
                    int ti = ((start + k) * width + x) * 4;
                    float wgt = weights[k];
                    b += tmp[ti] * wgt; g += tmp[ti + 1] * wgt; r += tmp[ti + 2] * wgt; a += tmp[ti + 3] * wgt;
                }
                int di = result.IndexOf(x, y);
                if (a < 0.5f) continue; // stays transparent
                if (a > 255f) a = 255f; // sharp filters can overshoot
                float inv = 255f / a;
                result.Data[di] = ColorMath.ClampByte(b * inv);
                result.Data[di + 1] = ColorMath.ClampByte(g * inv);
                result.Data[di + 2] = ColorMath.ClampByte(r * inv);
                result.Data[di + 3] = ColorMath.ClampByte(a);
            }
        });
        return result;
    }

    /// <summary>For each output index: first source index and normalized filter weights.</summary>
    private static (int Start, float[] Weights)[] BuildWeights(int sourceSize, int targetSize,
        (Func<double, double> Kernel, double Radius) filter)
    {
        double scale = (double)sourceSize / targetSize;
        double stretch = Math.Max(1.0, scale);      // widen the filter when shrinking
        double support = filter.Radius * stretch;
        var table = new (int, float[])[targetSize];

        for (int i = 0; i < targetSize; i++)
        {
            double center = (i + 0.5) * scale - 0.5;
            int start = Math.Max(0, (int)Math.Floor(center - support) + 1);
            int end = Math.Min(sourceSize - 1, (int)Math.Ceiling(center + support) - 1);
            if (end < start) start = end = Math.Clamp((int)Math.Round(center), 0, sourceSize - 1);

            var weights = new float[end - start + 1];
            double total = 0;
            for (int j = start; j <= end; j++)
            {
                double w = filter.Kernel((j - center) / stretch);
                weights[j - start] = (float)w;
                total += w;
            }
            if (Math.Abs(total) < 1e-9)
            {
                Array.Fill(weights, 1f / weights.Length);
            }
            else
            {
                for (int k = 0; k < weights.Length; k++) weights[k] = (float)(weights[k] / total);
            }
            table[i] = (start, weights);
        }
        return table;
    }
}
