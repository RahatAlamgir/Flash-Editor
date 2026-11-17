using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// Draws the shadow of an object that has already been rendered: the object's transparency is blurred,
/// shifted, tinted, and the object is laid on top of it.
/// </summary>
public static class ShadowFx
{
    /// <summary>
    /// Returns a new buffer of the same size: the shadow of <paramref name="source"/> with <paramref name="source"/>
    /// itself on top. Shadow parts that would fall outside the buffer are cut off, so give the buffer
    /// enough empty room around the object (see <see cref="ShadowStyle.Extent"/>).
    /// </summary>
    /// <param name="size">1 = the shadow is as big as the object, 0.5 = half as big, 2 = twice as big (around <paramref name="centerX"/>, <paramref name="centerY"/>).</param>
    /// <param name="centerX">Middle of the object in the buffer (the shadow grows and shrinks around it).</param>
    public static PixelBuffer Apply(PixelBuffer source, ColorRgba color, double offsetX, double offsetY, double blur,
        double size = 1.0, double centerX = 0, double centerY = 0)
    {
        int w = source.Width, h = source.Height;
        var alpha = new float[w * h];
        var data = source.Data;
        for (int p = 0; p < alpha.Length; p++) alpha[p] = data[p * 4 + 3] / 255f;

        if (Math.Abs(size - 1) > 0.005) alpha = Resize(alpha, w, h, size, centerX, centerY);
        if (blur > 0) alpha = Blur(alpha, w, h, sigma: blur / 2);

        int dx = (int)Math.Round(offsetX), dy = (int)Math.Round(offsetY);
        float strength = color.A / 255f;
        var result = new PixelBuffer(w, h);
        var output = result.Data;

        Parallel.For(0, h, y =>
        {
            int sy = y - dy;
            for (int x = 0; x < w; x++)
            {
                int sx = x - dx;
                float shadowA = sx >= 0 && sx < w && sy >= 0 && sy < h ? alpha[sy * w + sx] * strength : 0f;
                int i = (y * w + x) * 4;
                float objectA = data[i + 3] / 255f;
                float outA = objectA + shadowA * (1 - objectA);
                if (outA <= 0f) continue;

                // "Over" blend of the object on the shadow, on straight (non-premultiplied) colors.
                float shadowWeight = shadowA * (1 - objectA);
                output[i] = ColorMath.ClampByte((data[i] * objectA + color.B * shadowWeight) / outA);
                output[i + 1] = ColorMath.ClampByte((data[i + 1] * objectA + color.G * shadowWeight) / outA);
                output[i + 2] = ColorMath.ClampByte((data[i + 2] * objectA + color.R * shadowWeight) / outA);
                output[i + 3] = ColorMath.ClampByte(outA * 255f);
            }
        });
        return result;
    }

    /// <summary>The plane made bigger or smaller by <paramref name="size"/> around a point (smooth, bilinear).</summary>
    private static float[] Resize(float[] plane, int width, int height, double size, double cx, double cy)
    {
        var result = new float[plane.Length];
        double inverse = 1.0 / Math.Max(size, 0.01);
        Parallel.For(0, height, y =>
        {
            double sy = cy + (y + 0.5 - cy) * inverse - 0.5;
            int y0 = (int)Math.Floor(sy);
            double fy = sy - y0;
            for (int x = 0; x < width; x++)
            {
                double sx = cx + (x + 0.5 - cx) * inverse - 0.5;
                int x0 = (int)Math.Floor(sx);
                double fx = sx - x0;
                double sum = Sample(plane, width, height, x0, y0) * (1 - fx) * (1 - fy) + Sample(plane, width, height, x0 + 1, y0) * fx * (1 - fy)
                           + Sample(plane, width, height, x0, y0 + 1) * (1 - fx) * fy + Sample(plane, width, height, x0 + 1, y0 + 1) * fx * fy;
                result[y * width + x] = (float)sum;
            }
        });
        return result;
    }

    private static float Sample(float[] plane, int width, int height, int x, int y)
        => x < 0 || y < 0 || x >= width || y >= height ? 0f : plane[y * width + x];

    /// <summary>Soft blur of a 0..1 plane: three box blurs in each direction look like a Gaussian blur.</summary>
    internal static float[] Blur(float[] plane, int width, int height, double sigma)
    {
        // Box width that gives the wanted spread after three passes.
        int radius = Math.Max(1, (int)Math.Round((Math.Sqrt(4 * sigma * sigma + 1) - 1) / 2));
        var a = (float[])plane.Clone();
        var b = new float[plane.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            BoxBlur(a, b, width, height, radius, horizontal: true);  // a -> b
            BoxBlur(b, a, width, height, radius, horizontal: false); // b -> a
        }
        return a;
    }

    /// <summary>Averages each value with its neighbors within <paramref name="radius"/> (outside counts as 0).</summary>
    private static void BoxBlur(float[] src, float[] dst, int width, int height, int radius, bool horizontal)
    {
        int lines = horizontal ? height : width;
        int length = horizontal ? width : height;
        int step = horizontal ? 1 : width;
        int lineStep = horizontal ? width : 1;
        float norm = 1f / (2 * radius + 1);

        Parallel.For(0, lines, line =>
        {
            int start = line * lineStep;
            float sum = 0;
            for (int k = 0; k <= radius && k < length; k++) sum += src[start + k * step];
            for (int k = 0; k < length; k++)
            {
                dst[start + k * step] = sum * norm;
                int add = k + radius + 1, remove = k - radius;
                if (add < length) sum += src[start + add * step];
                if (remove >= 0) sum -= src[start + remove * step];
            }
        });
    }
}
