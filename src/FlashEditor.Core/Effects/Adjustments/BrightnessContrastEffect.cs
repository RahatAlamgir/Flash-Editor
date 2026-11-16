namespace FlashEditor.Core.Effects.Adjustments;

public sealed class BrightnessContrastEffect : PerPixelEffect
{
    public override string Name => "Brightness / Contrast";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("brightness", "Brightness", -100, 100, 0),
        new EffectParameter("contrast", "Contrast", -100, 100, 0),
    };

    protected override PixelMapper CreateMapper(EffectValues values)
    {
        double brightness = values["brightness"] * 1.5;          // shift of up to ±150 levels
        double c = values["contrast"] * 2.55;                     // -255..255
        double factor = 259 * (c + 255) / (255 * (259 - c));      // classic contrast curve
        var lut = BuildLut(v => (v + brightness - 128) * factor + 128);

        return (ref byte b, ref byte g, ref byte r) =>
        {
            b = lut[b]; g = lut[g]; r = lut[r];
        };
    }
}
