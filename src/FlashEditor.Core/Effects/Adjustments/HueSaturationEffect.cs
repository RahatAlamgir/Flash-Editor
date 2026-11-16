using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Adjustments;

public sealed class HueSaturationEffect : PerPixelEffect
{
    public override string Name => "Hue / Saturation";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("hue", "Hue", -180, 180, 0, 1, "°"),
        new EffectParameter("saturation", "Saturation", -100, 100, 0),
        new EffectParameter("lightness", "Lightness", -100, 100, 0),
    };

    protected override PixelMapper CreateMapper(EffectValues values)
    {
        double hueShift = values["hue"];
        double sat = values["saturation"] / 100.0;
        double light = values["lightness"] / 100.0;

        return (ref byte b, ref byte g, ref byte r) =>
        {
            ColorMath.RgbToHsl(r / 255.0, g / 255.0, b / 255.0, out double h, out double s, out double l);
            h += hueShift;
            // Positive saturation pushes towards fully saturated, negative towards gray.
            s = sat >= 0 ? s + (1 - s) * sat * s : s * (1 + sat);
            l = light >= 0 ? l + (1 - l) * light : l * (1 + light);
            ColorMath.HslToRgb(h, Math.Clamp(s, 0, 1), Math.Clamp(l, 0, 1), out double nr, out double ng, out double nb);
            r = ColorMath.ClampByte(nr * 255);
            g = ColorMath.ClampByte(ng * 255);
            b = ColorMath.ClampByte(nb * 255);
        };
    }
}
