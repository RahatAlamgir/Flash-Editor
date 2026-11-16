namespace FlashEditor.Core.Effects.Adjustments;

/// <summary>Exposure (in camera "stops") and gamma (midtone brightness).</summary>
public sealed class ExposureEffect : PerPixelEffect
{
    public override string Name => "Exposure / Gamma";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("exposure", "Exposure", -3, 3, 0, 0.05, "EV"),
        new EffectParameter("gamma", "Gamma (midtones)", 0.2, 3, 1, 0.01),
    };

    protected override PixelMapper CreateMapper(EffectValues values)
    {
        double gain = Math.Pow(2, values["exposure"]);
        double inverseGamma = 1.0 / Math.Max(0.01, values["gamma"]);
        var lut = BuildLut(v => 255 * Math.Pow(Math.Clamp(v / 255 * gain, 0, 1), inverseGamma));
        return (ref byte b, ref byte g, ref byte r) =>
        {
            b = lut[b]; g = lut[g]; r = lut[r];
        };
    }
}
