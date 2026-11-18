using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// Looks that are made from the transparency of an already drawn object: an outline around it, a glow behind it and a shadow inside its
/// edges. Give the buffer enough empty room around the object for outlines and glows (see the Extent of each style).
/// </summary>
public static class SurfaceFx
{
    /// <summary>The object with a line of <paramref name="width"/> pixels around its outside (the line is drawn behind the object).</summary>
    public static PixelBuffer Outline(PixelBuffer source, ColorRgba color, double width)
    {
        if (width <= 0 || color.A == 0) return source;
        int w = source.Width, h = source.Height;
        var alpha = ShadowFx.AlphaPlane(source);
        var content = ShadowFx.ContentWindow(alpha, w, h);
        if (content.IsEmpty) return source;

        int grow = (int)Math.Ceiling(width) + 2;
        var window = content.Grow(grow, grow, grow, grow, w, h);
        int ww = window.X1 - window.X0, wh = window.Y1 - window.Y0;

        // Distance of every pixel in the window to the nearest part of the object (that is more than half there).
        const float inf = 1e12f;
        var squared = new float[ww * wh];
        for (int y = 0; y < wh; y++)
            for (int x = 0; x < ww; x++)
                squared[y * ww + x] = alpha[(window.Y0 + y) * w + window.X0 + x] >= 0.5f ? 0f : inf;
        DistanceTransform(squared, ww, wh);

        var result = source.Clone();
        var data = result.Data;
        float lineAlpha = color.A / 255f;
        Parallel.For(0, wh, y =>
        {
            for (int x = 0; x < ww; x++)
            {
                float d = (float)Math.Sqrt(squared[y * ww + x]);
                float ring = Math.Clamp((float)width + 0.5f - d, 0f, 1f) * lineAlpha;
                if (ring <= 0f) continue;
                int i = ((window.Y0 + y) * w + window.X0 + x) * 4;
                float objectA = data[i + 3] / 255f;
                float outA = objectA + ring * (1 - objectA);
                float ringWeight = ring * (1 - objectA);
                data[i] = ColorMath.ClampByte((data[i] * objectA + color.B * ringWeight) / outA);
                data[i + 1] = ColorMath.ClampByte((data[i + 1] * objectA + color.G * ringWeight) / outA);
                data[i + 2] = ColorMath.ClampByte((data[i + 2] * objectA + color.R * ringWeight) / outA);
                data[i + 3] = ColorMath.ClampByte(outA * 255f);
            }
        });
        return result;
    }

    /// <summary>Only the glow of <paramref name="source"/> (a soft light around its outline), without the object. Lay it under the object with <see cref="Under"/>.</summary>
    public static PixelBuffer GlowLayer(PixelBuffer source, ColorRgba color, double size, double intensity)
    {
        int w = source.Width, h = source.Height;
        var result = new PixelBuffer(w, h);
        if (size <= 0 || color.A == 0) return result;
        var alpha = ShadowFx.AlphaPlane(source);
        var content = ShadowFx.ContentWindow(alpha, w, h);
        if (content.IsEmpty) return result;

        var soft = ShadowFx.Blur(alpha, w, h, sigma: size / 2);
        float boost = (float)Math.Max(0.1, intensity / 50.0);        // 100 % doubles the soft edge, so the light is strong near the object
        float strength = color.A / 255f;
        int reach = (int)Math.Ceiling(size * 1.5) + 2;
        var window = content.Grow(reach, reach, reach, reach, w, h);
        var data = result.Data;
        Parallel.For(window.Y0, window.Y1, y =>
        {
            for (int x = window.X0; x < window.X1; x++)
            {
                float a = Math.Min(1f, soft[y * w + x] * boost) * strength;
                if (a <= 0f) continue;
                int i = (y * w + x) * 4;
                data[i] = color.B; data[i + 1] = color.G; data[i + 2] = color.R; data[i + 3] = ColorMath.ClampByte(a * 255f);
            }
        });
        return result;
    }

    /// <summary>The picture <paramref name="top"/> laid over <paramref name="bottom"/> (same size).</summary>
    public static PixelBuffer Under(PixelBuffer top, PixelBuffer bottom)
    {
        int w = top.Width, h = top.Height;
        var result = bottom.Clone();
        var t = top.Data;
        var r = result.Data;
        Parallel.For(0, h, y =>
        {
            for (int p = y * w, end = p + w; p < end; p++)
            {
                int i = p * 4;
                float ta = t[i + 3] / 255f;
                if (ta <= 0f) continue;
                float ba = r[i + 3] / 255f;
                float outA = ta + ba * (1 - ta);
                float bw = ba * (1 - ta);
                r[i] = ColorMath.ClampByte((t[i] * ta + r[i] * bw) / outA);
                r[i + 1] = ColorMath.ClampByte((t[i + 1] * ta + r[i + 1] * bw) / outA);
                r[i + 2] = ColorMath.ClampByte((t[i + 2] * ta + r[i + 2] * bw) / outA);
                r[i + 3] = ColorMath.ClampByte(outA * 255f);
            }
        });
        return result;
    }

    /// <summary>A shadow inside the object's edges: the empty space around (and inside) the object is blurred, shifted and tinted onto the object.</summary>
    public static PixelBuffer InnerShadow(PixelBuffer source, ColorRgba color, double offsetX, double offsetY, double blur)
    {
        if (color.A == 0) return source;
        int w = source.Width, h = source.Height;
        var alpha = ShadowFx.AlphaPlane(source);
        var content = ShadowFx.ContentWindow(alpha, w, h);
        if (content.IsEmpty) return source;

        int dx = (int)Math.Round(offsetX), dy = (int)Math.Round(offsetY);
        double sigma = Math.Max(0, blur) / 2;
        int pad = (int)Math.Ceiling(sigma * 3) + Math.Max(Math.Abs(dx), Math.Abs(dy)) + 2;
        int pw = content.X1 - content.X0 + 2 * pad, ph = content.Y1 - content.Y0 + 2 * pad;

        // "What is not the object" in a plane that reaches beyond the object on every side (there is nothing beyond: it counts as empty).
        var empty = new float[pw * ph];
        Array.Fill(empty, 1f);
        for (int y = content.Y0; y < content.Y1; y++)
            for (int x = content.X0; x < content.X1; x++)
                empty[(y - content.Y0 + pad) * pw + (x - content.X0 + pad)] = 1f - alpha[y * w + x];
        var soft = sigma > 0 ? ShadowFx.Blur(empty, pw, ph, sigma) : empty;

        var result = source.Clone();
        var data = result.Data;
        float strength = color.A / 255f;
        Parallel.For(content.Y0, content.Y1, y =>
        {
            for (int x = content.X0; x < content.X1; x++)
            {
                int i = (y * w + x) * 4;
                if (data[i + 3] == 0) continue;
                int px = x - content.X0 + pad - dx, py = y - content.Y0 + pad - dy;
                float s = px >= 0 && px < pw && py >= 0 && py < ph ? soft[py * pw + px] : 1f;
                float k = s * strength;                                   // how much of the shadow color covers this pixel
                if (k <= 0f) continue;
                data[i] = ColorMath.ClampByte(data[i] + (color.B - data[i]) * k);
                data[i + 1] = ColorMath.ClampByte(data[i + 1] + (color.G - data[i + 1]) * k);
                data[i + 2] = ColorMath.ClampByte(data[i + 2] + (color.R - data[i + 2]) * k);
            }
        });
        return result;
    }

    // Felzenszwalb & Huttenlocher: squared distance to the nearest zero, in two passes (columns, then rows).
    private static void DistanceTransform(float[] f, int width, int height)
    {
        Parallel.For(0, width, x =>
        {
            var column = new float[height];
            for (int y = 0; y < height; y++) column[y] = f[y * width + x];
            var transformed = Transform1D(column);
            for (int y = 0; y < height; y++) f[y * width + x] = transformed[y];
        });
        Parallel.For(0, height, y =>
        {
            var row = new float[width];
            Array.Copy(f, y * width, row, 0, width);
            Array.Copy(Transform1D(row), 0, f, y * width, width);
        });
    }

    private static float[] Transform1D(float[] f)
    {
        int n = f.Length;
        var d = new float[n];
        var v = new int[n];
        var z = new float[n + 1];
        int k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            float s;
            while (true)
            {
                int p = v[k];
                s = ((f[q] + (float)q * q) - (f[p] + (float)p * p)) / (2f * q - 2f * p);
                if (s <= z[k] && k > 0) { k--; continue; }
                break;
            }
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            float dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
        return d;
    }
}
