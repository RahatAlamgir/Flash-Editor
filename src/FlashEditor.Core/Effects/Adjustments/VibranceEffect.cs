using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Adjustments;

/// <summary>
/// "Smart saturation": boosts dull colors more than already-strong ones, so skin tones and
/// bright colors don't get garish.
/// </summary>
public sealed class VibranceEffect : PerPixelEffect
{
    public override string Name => "Vibrance";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("vibrance", "Vibrance", -100, 100, 40),
    };

    protected override PixelMapper CreateMapper(EffectValues values)
    {
        double amount = values["vibrance"] / 100.0;
        return (ref byte b, ref byte g, ref byte r) =>
        {
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            double saturation = max == 0 ? 0 : (max - min) / (double)max;
            // Strong boost for dull pixels, little for saturated ones.
            double scale = 1 + amount * (1 - saturation) * (amount > 0 ? 1.5 : 1);
            double gray = 0.299 * r + 0.587 * g + 0.114 * b;
            r = ColorMath.ClampByte(gray + (r - gray) * scale);
            g = ColorMath.ClampByte(gray + (g - gray) * scale);
            b = ColorMath.ClampByte(gray + (b - gray) * scale);
        };
    }
}
