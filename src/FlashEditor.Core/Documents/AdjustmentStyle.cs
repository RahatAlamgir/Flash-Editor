using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Documents;

/// <summary>
/// The settings of an adjustment layer: a layer that has no picture of its own but changes the colors of every layer below it.
/// Nothing is ever changed in the pictures themselves, so it can be tuned or removed at any time.
/// </summary>
/// <param name="Brightness">-100 (darker) .. 100 (lighter).</param>
/// <param name="Contrast">-100 (flatter) .. 100 (stronger).</param>
/// <param name="Saturation">-100 (grey) .. 100 (more vivid).</param>
/// <param name="Hue">-180 .. 180 degrees around the color wheel.</param>
public sealed record AdjustmentStyle(double Brightness = 0, double Contrast = 0, double Saturation = 0, double Hue = 0)
{
    public static AdjustmentStyle Neutral { get; } = new();

    /// <summary>True when applying it would change nothing.</summary>
    public bool IsNeutral => Brightness == 0 && Contrast == 0 && Saturation == 0 && Hue == 0;

    /// <summary>The adjusted color of one pixel (channels 0..255), mixed with the original by <paramref name="strength"/> (0..1).</summary>
    public void Apply(ref byte b, ref byte g, ref byte r, double strength = 1)
    {
        if (IsNeutral || strength <= 0) return;
        double rr = r / 255.0, gg = g / 255.0, bb = b / 255.0;
        double or = rr, og = gg, ob = bb;

        if (Hue != 0 || Saturation != 0)
        {
            ColorMath.RgbToHsv(rr, gg, bb, out double h, out double s, out double v);
            if (Hue != 0) h += Hue;
            if (Saturation != 0) s = Math.Clamp(Saturation > 0 ? s + (1 - s) * Saturation / 100 * Math.Min(1, s * 2 + 0.2) : s * (1 + Saturation / 100), 0, 1);
            ColorMath.HsvToRgb(h, s, v, out rr, out gg, out bb);
        }

        if (Contrast != 0)
        {
            double k = Contrast > 0 ? 1 + Contrast / 100 * 1.5 : 1 + Contrast / 100;      // up to 2.5 times steeper, down to flat grey
            rr = (rr - 0.5) * k + 0.5; gg = (gg - 0.5) * k + 0.5; bb = (bb - 0.5) * k + 0.5;
        }
        if (Brightness != 0)
        {
            double add = Brightness / 100 * 0.5;
            rr += add; gg += add; bb += add;
        }

        r = ColorMath.ClampByte((or + (rr - or) * strength) * 255);
        g = ColorMath.ClampByte((og + (gg - og) * strength) * 255);
        b = ColorMath.ClampByte((ob + (bb - ob) * strength) * 255);
    }

    /// <summary>Adjusts the pixels inside <paramref name="region"/> of a picture in place (transparent pixels stay as they are).</summary>
    public void Apply(PixelBuffer target, PixelRect region, double strength = 1)
    {
        region = region.Intersect(target.Bounds);
        if (IsNeutral || strength <= 0 || region.IsEmpty) return;
        var data = target.Data;
        Parallel.For(region.Y, region.Bottom, y =>
        {
            int i = target.IndexOf(region.X, y);
            for (int x = 0; x < region.Width; x++, i += 4)
            {
                if (data[i + 3] == 0) continue;
                Apply(ref data[i], ref data[i + 1], ref data[i + 2], strength);
            }
        });
    }
}
