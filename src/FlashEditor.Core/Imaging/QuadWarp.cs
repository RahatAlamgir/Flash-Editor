namespace FlashEditor.Core.Imaging;

/// <summary>
/// Stretches a picture onto any four-sided shape (a perspective warp): the picture's corners are moved to four new
/// places. Used to reshape pictures, text, groups and curved shapes by dragging their corners.
/// </summary>
public sealed class QuadWarp
{
    // Maps a point of the quad back to the unit square: (u,v) = (m0 x + m1 y + m2, m3 x + m4 y + m5) / (m6 x + m7 y + m8).
    private readonly double[] _m;

    private QuadWarp(double[] inverse) => _m = inverse;

    /// <summary>
    /// The warp that sends the unit square (0,0 top-left, 1,0, 1,1, 0,1) to the four points (top-left, top-right,
    /// bottom-right, bottom-left), or null when the four points do not make a usable shape (all on one line...).
    /// </summary>
    public static QuadWarp? Create(IReadOnlyList<PointD> quad)
    {
        if (quad.Count != 4) return null;
        double x0 = quad[0].X, y0 = quad[0].Y, x1 = quad[1].X, y1 = quad[1].Y, x2 = quad[2].X, y2 = quad[2].Y, x3 = quad[3].X, y3 = quad[3].Y;
        double dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        double dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
        double denominator = dx1 * dy2 - dy1 * dx2;
        double g, h;
        if (Math.Abs(dx3) < 1e-12 && Math.Abs(dy3) < 1e-12) { g = 0; h = 0; }       // a parallelogram: no perspective
        else
        {
            if (Math.Abs(denominator) < 1e-12) return null;
            g = (dx3 * dy2 - dy3 * dx2) / denominator;
            h = (dx1 * dy3 - dy1 * dx3) / denominator;
        }
        double a = x1 - x0 + g * x1, b = x3 - x0 + h * x3, c = x0;
        double d = y1 - y0 + g * y1, e = y3 - y0 + h * y3, f = y0;

        // Inverse of [[a b c][d e f][g h 1]] by its adjugate (the scale does not matter).
        double[] inv =
        {
            e - f * h, c * h - b, b * f - c * e,
            f * g - d, a - c * g, c * d - a * f,
            d * h - e * g, b * g - a * h, a * e - b * d,
        };
        if (Math.Abs(inv[8]) < 1e-12 && Math.Abs(inv[6]) < 1e-12 && Math.Abs(inv[7]) < 1e-12) return null;
        return new QuadWarp(inv);
    }

    /// <summary>The place in the unit square that lands on (x, y); false when (x, y) is beyond the horizon of the perspective.</summary>
    public bool Unmap(double x, double y, out double u, out double v)
    {
        double w = _m[6] * x + _m[7] * y + _m[8];
        if (Math.Abs(w) < 1e-12) { u = v = 0; return false; }
        u = (_m[0] * x + _m[1] * y + _m[2]) / w;
        v = (_m[3] * x + _m[4] * y + _m[5]) / w;
        return true;
    }

    /// <summary>
    /// Draws <paramref name="source"/> warped. The part of the source that is stretched onto the quad is the rectangle
    /// (<paramref name="srcX"/>, <paramref name="srcY"/>, <paramref name="srcW"/>, <paramref name="srcH"/>) in source pixels (the rest of
    /// the source, e.g. a border around the picture, is drawn along around the quad). The result has the given size; the quad was given
    /// in its pixel coordinates.
    /// </summary>
    public PixelBuffer Apply(PixelBuffer source, double srcX, double srcY, double srcW, double srcH, int width, int height)
    {
        var result = new PixelBuffer(width, height);
        var src = source.Data;
        int sw = source.Width, sh = source.Height;
        var dst = result.Data;

        Parallel.For(0, height, py =>
        {
            for (int px = 0; px < width; px++)
            {
                if (!Unmap(px + 0.5, py + 0.5, out double u, out double v)) continue;
                double sx = srcX + u * srcW - 0.5, sy = srcY + v * srcH - 0.5;
                if (sx < -1 || sy < -1 || sx > sw || sy > sh || double.IsNaN(sx) || double.IsNaN(sy)) continue;

                int x0 = (int)Math.Floor(sx), y0 = (int)Math.Floor(sy);
                double fx = sx - x0, fy = sy - y0;
                double a = 0, r = 0, g = 0, b = 0;
                for (int k = 0; k < 4; k++)
                {
                    int cx = x0 + (k & 1), cy = y0 + (k >> 1);
                    if (cx < 0 || cy < 0 || cx >= sw || cy >= sh) continue;
                    double weight = ((k & 1) == 0 ? 1 - fx : fx) * ((k >> 1) == 0 ? 1 - fy : fy);
                    int i = (cy * sw + cx) * 4;
                    double alpha = src[i + 3] * weight;      // premultiplied so transparent pixels do not tint the edges
                    a += alpha;
                    b += src[i] * alpha; g += src[i + 1] * alpha; r += src[i + 2] * alpha;
                }
                if (a < 0.5) continue;
                int o = (py * width + px) * 4;
                dst[o] = (byte)Math.Clamp(b / a + 0.5, 0, 255);
                dst[o + 1] = (byte)Math.Clamp(g / a + 0.5, 0, 255);
                dst[o + 2] = (byte)Math.Clamp(r / a + 0.5, 0, 255);
                dst[o + 3] = (byte)Math.Clamp(a + 0.5, 0, 255);
            }
        });
        return result;
    }
}
