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
    public static PixelBuffer Apply(PixelBuffer source, ColorRgba color, double offsetX, double offsetY, double blur)
    {
        int w = source.Width, h = source.Height;
        var alpha = new float[w * h];
        var data = source.Data;
        for (int p = 0; p < alpha.Length; p++) alpha[p] = data[p * 4 + 3] / 255f;

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

    /// <summary>Soft blur of a 0..1 plane: three box blurs in each direction look like a Gaussian blur.</summary>
    private static float[] Blur(float[] plane, int width, int height, double sigma)
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
