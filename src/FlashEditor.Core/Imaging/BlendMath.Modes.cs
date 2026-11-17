namespace FlashEditor.Core.Imaging;

/// <summary>Layer blend modes (the formulas of the W3C compositing standard) on straight-alpha BGRA bytes.</summary>
public static partial class BlendMath
{
    /// <summary>
    /// Paints a source pixel onto the destination using a blend mode. <paramref name="opacity"/> scales the source's own alpha.
    /// <see cref="BlendMode.Normal"/> is the same as <see cref="OverPixel"/>.
    /// </summary>
    public static void BlendPixel(byte[] dst, int di, byte[] src, int si, double opacity, BlendMode mode)
    {
        if (mode == BlendMode.Normal) { OverPixel(dst, di, src, si, opacity); return; }

        double sa = src[si + 3] / 255.0 * opacity;
        if (sa <= 0) return;
        double ba = dst[di + 3] / 255.0;
        if (ba <= 0) { OverPixel(dst, di, src, si, opacity); return; }       // nothing below to blend with

        double outA = sa + ba * (1 - sa);
        for (int c = 0; c < 3; c++)
        {
            double cb = dst[di + c] / 255.0, cs = src[si + c] / 255.0;
            double mixed = Channel(mode, cb, cs);
            double value = ((1 - sa) * ba * cb + (1 - ba) * sa * cs + sa * ba * mixed) / outA;
            dst[di + c] = ColorMath.ClampByte(value * 255);
        }
        dst[di + 3] = ColorMath.ClampByte(outA * 255);
    }

    /// <summary>The blend function B(backdrop, source) for one color channel (0..1).</summary>
    public static double Channel(BlendMode mode, double cb, double cs) => mode switch
    {
        BlendMode.Multiply => cb * cs,
        BlendMode.Screen => cb + cs - cb * cs,
        BlendMode.Overlay => HardLightOf(cs, cb),
        BlendMode.HardLight => HardLightOf(cb, cs),
        BlendMode.SoftLight => SoftLightOf(cb, cs),
        BlendMode.Darken => Math.Min(cb, cs),
        BlendMode.Lighten => Math.Max(cb, cs),
        BlendMode.Difference => Math.Abs(cb - cs),
        _ => cs,
    };

    private static double HardLightOf(double cb, double cs)
        => cs <= 0.5 ? cb * 2 * cs : cb + (2 * cs - 1) - cb * (2 * cs - 1);

    private static double SoftLightOf(double cb, double cs)
    {
        if (cs <= 0.5) return cb - (1 - 2 * cs) * cb * (1 - cb);
        double d = cb <= 0.25 ? ((16 * cb - 12) * cb + 4) * cb : Math.Sqrt(cb);
        return cb + (2 * cs - 1) * (d - cb);
    }
}
