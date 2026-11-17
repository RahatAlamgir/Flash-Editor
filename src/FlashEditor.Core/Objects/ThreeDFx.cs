using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// Draws the 3D look of an object that has already been rendered: a bevel on its face, and a solid side behind it made by
/// pulling the object's outline along a direction (an extrusion).
/// </summary>
public static class ThreeDFx
{
    private const float Far = 1e9f;

    /// <summary>
    /// Returns a new buffer of the same size. Parts of the side that would fall outside the buffer are cut off, so give the buffer
    /// enough empty room around the object (see <see cref="ThreeDStyle.Extent"/>).
    /// </summary>
    /// <param name="offsetX">The way the side runs, in pixels: its length is the depth.</param>
    /// <param name="color">Color of the side, or null to use the object's own color, darker.</param>
    /// <param name="bevel">0..1 strength of the lit / shaded edges.</param>
    /// <param name="bevelSize">Width of the lit and shaded edges in pixels.</param>
    public static PixelBuffer Apply(PixelBuffer source, double offsetX, double offsetY, ColorRgba? color, double bevel, double bevelSize)
    {
        int w = source.Width, h = source.Height;
        var data = source.Data;
        var alpha = ShadowFx.AlphaPlane(source);

        // Only the object and the way its side runs can hold anything; the rest of the buffer stays empty and is never visited.
        var face = ShadowFx.ContentWindow(alpha, w, h);
        if (face.IsEmpty) { return new PixelBuffer(w, h); }

        var front = bevel > 0.001 ? Bevelled(source, alpha, bevel, bevelSize, face) : source.Data;

        int depth = (int)Math.Round(Math.Sqrt(offsetX * offsetX + offsetY * offsetY));
        float[]? side = null, distance = null;
        var work = face;
        if (depth >= 1)
        {
            int sx = (int)Math.Ceiling(Math.Abs(offsetX)) + 2, sy = (int)Math.Ceiling(Math.Abs(offsetY)) + 2;
            work = face.Grow(offsetX < 0 ? sx : 0, offsetY < 0 ? sy : 0, offsetX > 0 ? sx : 0, offsetY > 0 ? sy : 0, w, h);
            Extrude(alpha, w, h, offsetX, offsetY, depth, work, out side, out distance);
        }

        var result = new PixelBuffer(w, h);
        var output = result.Data;
        var sideColor = color ?? AutoColor(source, alpha, w * h);

        Parallel.For(work.Y0, work.Y1, y =>
        {
            for (int x = work.X0; x < work.X1; x++)
            {
                int p = y * w + x, i = p * 4;
                float frontA = alpha[p];
                float sideA = side != null ? side[p] : 0f;
                float shade = distance != null ? 1f - 0.45f * Math.Min(1f, distance[p] / Math.Max(1, depth)) : 1f;   // darker the deeper it goes
                float outA = frontA + sideA * (1 - frontA);
                if (outA <= 0f) continue;

                float sideWeight = sideA * (1 - frontA);
                output[i] = ColorMath.ClampByte((front[i] * frontA + sideColor.B * shade * sideWeight) / outA);
                output[i + 1] = ColorMath.ClampByte((front[i + 1] * frontA + sideColor.G * shade * sideWeight) / outA);
                output[i + 2] = ColorMath.ClampByte((front[i + 2] * frontA + sideColor.R * shade * sideWeight) / outA);
                output[i + 3] = ColorMath.ClampByte(outA * 255f);
            }
        });
        return result;
    }

    /// <summary>The object's own color (average of what is visible), made darker, for the side.</summary>
    private static ColorRgba AutoColor(PixelBuffer source, float[] alpha, int count)
    {
        double r = 0, g = 0, b = 0, total = 0;
        var d = source.Data;
        for (int p = 0; p < count; p += 3)          // every third pixel is plenty
        {
            double a = alpha[p];
            if (a <= 0) continue;
            b += d[p * 4] * a; g += d[p * 4 + 1] * a; r += d[p * 4 + 2] * a; total += a;
        }
        if (total <= 0) return new ColorRgba(60, 60, 60);
        const double dark = 0.55;
        return new ColorRgba(ColorMath.ClampByte(r / total * dark), ColorMath.ClampByte(g / total * dark), ColorMath.ClampByte(b / total * dark));
    }

    /// <summary>
    /// Pulls the outline along the direction: <paramref name="side"/> is the coverage of the extruded outline (0..1) and
    /// <paramref name="distance"/> how far each pixel is from the face along the way. Done by doubling (each pass doubles the length
    /// covered), so even a very deep side needs only a handful of passes.
    /// </summary>
    private static void Extrude(float[] alpha, int w, int h, double offsetX, double offsetY, int depth, ShadowFx.Window window,
        out float[] side, out float[] distance)
    {
        double length = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
        double ux = offsetX / length, uy = offsetY / length;

        // Two pairs of buffers swap roles every pass (no new buffers per pass). Outside the window nothing is ever written, so they
        // keep their start values: no coverage and "far away".
        int count = w * h;
        var e = new float[count];
        Array.Copy(alpha, e, count);
        var d = new float[count];
        var e2 = new float[count];
        var d2 = new float[count];
        Array.Fill(d, Far, 0, count);
        Array.Fill(d2, Far, 0, count);
        for (int y = window.Y0; y < window.Y1; y++)
            for (int x = window.X0, p = y * w + window.X0; x < window.X1; x++, p++)
                if (alpha[p] > 0.004f) d[p] = 0f;

        int covered = 1;                                      // the face plus (covered - 1) steps along the way
        while (covered * 2 <= depth + 1)
        {
            Doubled(e, d, e2, d2, w, window, (int)Math.Round(ux * covered), (int)Math.Round(uy * covered), covered);
            (e, e2) = (e2, e);
            (d, d2) = (d2, d);
            covered *= 2;
        }
        if (covered < depth + 1)
        {
            int rest = depth + 1 - covered;
            Doubled(e, d, e2, d2, w, window, (int)Math.Round(ux * rest), (int)Math.Round(uy * rest), rest);
            (e, e2) = (e2, e);
            (d, d2) = (d2, d);
        }
        side = e;
        distance = d;
    }

    /// <summary>Each pixel also takes what lies <paramref name="steps"/> steps behind it (at (dx, dy)): more coverage, a longer way.</summary>
    private static void Doubled(float[] e, float[] d, float[] ne, float[] nd, int w, ShadowFx.Window window, int dx, int dy, int steps)
    {
        int h = window.Y1;
        Parallel.For(window.Y0, window.Y1, y =>
        {
            int sy = y - dy;
            for (int x = window.X0; x < window.X1; x++)
            {
                int p = y * w + x, sx = x - dx;
                float coverage = e[p], way = d[p];
                if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                {
                    int q = sy * w + sx;
                    if (e[q] > coverage) coverage = e[q];
                    if (d[q] < Far && d[q] + steps < way) way = d[q] + steps;
                }
                ne[p] = coverage;
                nd[p] = way;
            }
        });
    }

    /// <summary>
    /// The face with a lit top-left edge and a shaded bottom-right edge, as if it were raised. The edge is found from the slope of the
    /// blurred transparency.
    /// </summary>
    private static byte[] Bevelled(PixelBuffer source, float[] alpha, double strength, double size, ShadowFx.Window face)
    {
        int w = source.Width, h = source.Height;
        var soft = ShadowFx.Blur(alpha, w, h, sigma: Math.Max(0.8, size / 2));
        double reach = Math.Max(0.8, size / 2);
        var output = (byte[])source.Data.Clone();
        const double lx = -0.7071, ly = -0.7071;              // the light comes from the top left

        Parallel.For(Math.Max(1, face.Y0), Math.Min(h - 1, face.Y1), y =>
        {
            for (int x = Math.Max(1, face.X0); x < Math.Min(w - 1, face.X1); x++)
            {
                int p = y * w + x, i = p * 4;
                if (alpha[p] <= 0.004f) continue;
                double gx = (soft[p + 1] - soft[p - 1]) / 2.0, gy = (soft[p + w] - soft[p - w]) / 2.0;
                double magnitude = Math.Sqrt(gx * gx + gy * gy);
                if (magnitude < 1e-4) continue;

                double lit = (-gx * lx + -gy * ly) / magnitude;                    // 1 on edges facing the light, -1 on the opposite ones
                double edge = Math.Clamp(magnitude * reach * 2.4, 0, 1);            // 0 in flat areas, 1 right at the edge
                double amount = lit * edge * strength;
                for (int c = 0; c < 3; c++)
                {
                    double value = output[i + c];
                    value = amount > 0 ? value + (255 - value) * amount * 0.85 : value * (1 + amount * 0.75);
                    output[i + c] = ColorMath.ClampByte(value);
                }
            }
        });
        return output;
    }
}
