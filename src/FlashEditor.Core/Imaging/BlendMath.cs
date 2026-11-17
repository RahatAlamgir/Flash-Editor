namespace FlashEditor.Core.Imaging;

/// <summary>Per-pixel blending helpers that work on straight-alpha BGRA byte arrays.</summary>
public static partial class BlendMath
{
    /// <summary>
    /// Paints a color "over" the destination pixel. <paramref name="alpha"/> is the effective source
    /// opacity in 0..1 (color alpha × brush coverage × layer opacity, etc).
    /// </summary>
    public static void Over(byte[] dst, int i, byte b, byte g, byte r, double alpha)
    {
        if (alpha <= 0) return;
        if (alpha >= 1)
        {
            dst[i] = b; dst[i + 1] = g; dst[i + 2] = r; dst[i + 3] = 255;
            return;
        }

        double da = dst[i + 3] / 255.0;
        double keep = da * (1 - alpha);
        double outA = alpha + keep;
        if (outA <= 0) return;

        dst[i] = ColorMath.ClampByte((b * alpha + dst[i] * keep) / outA);
        dst[i + 1] = ColorMath.ClampByte((g * alpha + dst[i + 1] * keep) / outA);
        dst[i + 2] = ColorMath.ClampByte((r * alpha + dst[i + 2] * keep) / outA);
        dst[i + 3] = ColorMath.ClampByte(outA * 255);
    }

    /// <summary>Paints source pixel (straight alpha) over destination, scaled by <paramref name="opacity"/>.</summary>
    public static void OverPixel(byte[] dst, int di, byte[] src, int si, double opacity)
        => Over(dst, di, src[si], src[si + 1], src[si + 2], src[si + 3] / 255.0 * opacity);

    /// <summary>
    /// Mixes pixel <paramref name="a"/> towards pixel <paramref name="b"/> by <paramref name="t"/> (0..1),
    /// using premultiplied math so transparent pixels don't leak dark fringes. Safe if dst aliases a or b.
    /// </summary>
    public static void Lerp(byte[] dst, int di, byte[] a, int ai, byte[] b, int bi, double t)
    {
        if (t <= 0) { Copy(dst, di, a, ai); return; }
        if (t >= 1) { Copy(dst, di, b, bi); return; }

        double aa = a[ai + 3], ba = b[bi + 3];
        double outA = aa + (ba - aa) * t;
        if (outA < 0.5)
        {
            dst[di] = dst[di + 1] = dst[di + 2] = dst[di + 3] = 0;
            return;
        }
        double c0 = (a[ai] * aa + (b[bi] * ba - a[ai] * aa) * t) / outA;
        double c1 = (a[ai + 1] * aa + (b[bi + 1] * ba - a[ai + 1] * aa) * t) / outA;
        double c2 = (a[ai + 2] * aa + (b[bi + 2] * ba - a[ai + 2] * aa) * t) / outA;
        dst[di] = ColorMath.ClampByte(c0);
        dst[di + 1] = ColorMath.ClampByte(c1);
        dst[di + 2] = ColorMath.ClampByte(c2);
        dst[di + 3] = ColorMath.ClampByte(outA);
    }

    private static void Copy(byte[] dst, int di, byte[] src, int si)
    {
        if (ReferenceEquals(dst, src) && di == si) return;
        dst[di] = src[si]; dst[di + 1] = src[si + 1]; dst[di + 2] = src[si + 2]; dst[di + 3] = src[si + 3];
    }
}
