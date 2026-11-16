namespace FlashEditor.Core.Effects.Adjustments;

/// <summary>White balance: warmer (orange) or cooler (blue), and a green / magenta tint.</summary>
public sealed class TemperatureEffect : PerPixelEffect
{
    public override string Name => "Temperature / Tint";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("temperature", "Temperature (cool / warm)", -100, 100, 0),
        new EffectParameter("tint", "Tint (green / magenta)", -100, 100, 0),
    };

    protected override PixelMapper CreateMapper(EffectValues values)
    {
        double t = values["temperature"] / 100.0 * 40; // up to ±40 levels
        double tint = values["tint"] / 100.0 * 30;
        var red = BuildLut(v => v + t + tint * 0.5);
        var green = BuildLut(v => v - tint);
        var blue = BuildLut(v => v - t + tint * 0.5);
        return (ref byte b, ref byte g, ref byte r) =>
        {
            b = blue[b]; g = green[g]; r = red[r];
        };
    }
}
