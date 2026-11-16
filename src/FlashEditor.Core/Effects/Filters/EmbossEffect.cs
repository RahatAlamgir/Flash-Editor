using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Makes the picture look stamped into gray metal or paper.</summary>
public sealed class EmbossEffect : ImageEffect
{
    public override string Name => "Emboss";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("strength", "Strength", 0.2, 5, 1.5, 0.1),
        EffectParameter.Toggle("color", "Keep original colors", false),
    };

    public override int GetPadding(EffectValues values) => 1;

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double strength = values["strength"];
        var embossed = Convolution.MapLuma3x3(source, n =>
        {
            // Light from the top-left.
            double v = 128 + strength * (n[8] + n[5] + n[7] - n[0] - n[1] - n[3]) / 3;
            byte g = ColorMath.ClampByte(v);
            return (g, g, g);
        }, cancellation);

        if (values["color"] < 0.5) return embossed;

        // Overlay the relief on the original colors.
        var result = source.Clone();
        var d = result.Data;
        var e = embossed.Data;
        for (int i = 0; i < d.Length; i += 4)
        {
            double shift = e[i] - 128;
            d[i] = ColorMath.ClampByte(d[i] + shift);
            d[i + 1] = ColorMath.ClampByte(d[i + 1] + shift);
            d[i + 2] = ColorMath.ClampByte(d[i + 2] + shift);
        }
        return result;
    }
}
