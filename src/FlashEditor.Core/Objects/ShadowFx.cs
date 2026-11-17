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
        var alpha = AlphaPlane(source);
        var data = source.Data;

        // Where anything can end up: the object itself, and its shadow (shifted and spread by the blur). Nothing else is visited.
        var objectWindow = ContentWindow(alpha, w, h);
        if (objectWindow.IsEmpty) { return new PixelBuffer(w, h); }
        int dx = (int)Math.Round(offsetX), dy = (int)Math.Round(offsetY);
        int spread = blur > 0 ? Math.Max(1, (int)Math.Round((Math.Sqrt(blur * blur + 1) - 1) / 2)) * 3 + 2 : 0;
        bool resized = Math.Abs(size - 1) > 0.005;
        var shadowWindow = resized
            ? new Window(0, 0, w, h)                                           // a resized shadow can reach anywhere around the center
            : objectWindow.Grow(spread - dx, spread - dy, spread + dx, spread + dy, w, h);
        var window = new Window(Math.Min(objectWindow.X0, shadowWindow.X0), Math.Min(objectWindow.Y0, shadowWindow.Y0),
                                Math.Max(objectWindow.X1, shadowWindow.X1), Math.Max(objectWindow.Y1, shadowWindow.Y1));
        if (resized) { var bigger = Resize(alpha, w, h, size, centerX, centerY); alpha = bigger; }
        if (blur > 0) { var soft = Blur(alpha, w, h, sigma: blur / 2); alpha = soft; }
        float strength = color.A / 255f;
        var result = new PixelBuffer(w, h);
        var output = result.Data;

        Parallel.For(window.Y0, window.Y1, y =>
        {
            int sy = y - dy;
            for (int x = window.X0; x < window.X1; x++)
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

    /// <summary>The transparency of a picture as a 0..1 plane.</summary>
    internal static float[] AlphaPlane(PixelBuffer source)
    {
        int w = source.Width, h = source.Height;
        var alpha = new float[w * h];
        var data = source.Data;
        Parallel.For(0, h, y =>
        {
            for (int p = y * w, end = p + w; p < end; p++) alpha[p] = data[p * 4 + 3] / 255f;
        });
        return alpha;
    }

    /// <summary>The plane made bigger or smaller by <paramref name="size"/> around a point (smooth, bilinear).</summary>
    private static float[] Resize(float[] plane, int width, int height, double size, double cx, double cy)
    {
        var result = new float[width * height];
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

    /// <summary>Rows and columns that hold something: [X0, X1) x [Y0, Y1). Empty when the plane is all zero.</summary>
    internal readonly record struct Window(int X0, int Y0, int X1, int Y1)
    {
        public bool IsEmpty => X1 <= X0 || Y1 <= Y0;
        public Window Grow(int left, int top, int right, int bottom, int width, int height)
            => new(Math.Max(0, X0 - left), Math.Max(0, Y0 - top), Math.Min(width, X1 + right), Math.Min(height, Y1 + bottom));
    }

    /// <summary>The smallest window around everything in the plane that is above <paramref name="threshold"/>.</summary>
    internal static Window ContentWindow(float[] plane, int width, int height, float threshold = 0f)
    {
        int minX = width, maxX = -1, minY = height, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            var row = plane.AsSpan(y * width, width);
            int first, last;
            if (threshold <= 0f)
            {
                first = row.IndexOfAnyExcept(0f);          // vectorized: skips empty rows many pixels at a time
                if (first < 0) continue;
                last = row.LastIndexOfAnyExcept(0f);
            }
            else
            {
                first = -1;
                for (int x = 0; x < row.Length; x++) if (row[x] > threshold) { first = x; break; }
                if (first < 0) continue;
                last = row.Length - 1;
                while (row[last] <= threshold) last--;
            }
            if (first < minX) minX = first;
            if (last > maxX) maxX = last;
            if (y < minY) minY = y;
            maxY = y;
        }
        return maxX < 0 ? new Window(0, 0, 0, 0) : new Window(minX, minY, maxX + 1, maxY + 1);
    }

    /// <summary>Soft blur of a 0..1 plane: three box blurs in each direction look like a Gaussian blur.</summary>
    internal static float[] Blur(float[] plane, int width, int height, double sigma)
    {
        // Box width that gives the wanted spread after three passes.
        int radius = Math.Max(1, (int)Math.Round((Math.Sqrt(4 * sigma * sigma + 1) - 1) / 2));
        int count = width * height;
        var a = new float[count];
        Array.Copy(plane, a, count);

        // Everything outside the content (grown by what three passes can spread) stays zero, so it is never visited.
        var content = ContentWindow(plane, width, height);
        if (content.IsEmpty) return a;
        int reach = radius * 3;
        var window = content.Grow(reach, reach, reach, reach, width, height);

        var b = new float[count];
        for (int pass = 0; pass < 3; pass++)
        {
            BoxBlurHorizontal(a, b, width, window, radius);  // a -> b
            BoxBlurVertical(b, a, width, window, radius);    // b -> a
        }
        return a;
    }

    /// <summary>Averages each value with its neighbors within <paramref name="radius"/> along the row (outside counts as 0).</summary>
    private static void BoxBlurHorizontal(float[] src, float[] dst, int width, Window win, int radius)
    {
        int length = win.X1 - win.X0;
        float norm = 1f / (2 * radius + 1);

        Parallel.For(win.Y0, win.Y1, y =>
        {
            int start = y * width + win.X0;
            float sum = 0;
            for (int k = 0; k <= radius && k < length; k++) sum += src[start + k];
            for (int k = 0; k < length; k++)
            {
                dst[start + k] = sum * norm;
                int add = k + radius + 1, remove = k - radius;
                if (add < length) sum += src[start + add];
                if (remove >= 0) sum -= src[start + remove];
            }
        });
    }

    /// <summary>
    /// The same along the columns. It walks down the picture row by row for a strip of columns at a time, so memory is read in order
    /// (walking down one column at a time jumps a whole row of memory with every step, which is several times slower).
    /// </summary>
    private static void BoxBlurVertical(float[] src, float[] dst, int width, Window win, int radius)
    {
        int length = win.Y1 - win.Y0;
        float norm = 1f / (2 * radius + 1);
        const int strip = 16;
        int strips = (win.X1 - win.X0 + strip - 1) / strip;

        Parallel.For(0, strips, s =>
        {
            int x0 = win.X0 + s * strip, n = Math.Min(win.X1, x0 + strip) - x0;
            var sum = new float[n];
            for (int k = 0; k <= radius && k < length; k++)
            {
                int row = (win.Y0 + k) * width + x0;
                for (int i = 0; i < n; i++) sum[i] += src[row + i];
            }
            for (int k = 0; k < length; k++)
            {
                int row = (win.Y0 + k) * width + x0;
                for (int i = 0; i < n; i++) dst[row + i] = sum[i] * norm;
                int add = k + radius + 1, remove = k - radius;
                if (add < length)
                {
                    int r = (win.Y0 + add) * width + x0;
                    for (int i = 0; i < n; i++) sum[i] += src[r + i];
                }
                if (remove >= 0)
                {
                    int r = (win.Y0 + remove) * width + x0;
                    for (int i = 0; i < n; i++) sum[i] -= src[r + i];
                }
            }
        });
    }
}
