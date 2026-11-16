namespace FlashEditor.Core.Effects.Adjustments;

/// <summary>Reduces each color channel to a few levels (poster / cartoon look).</summary>
public sealed class PosterizeEffect : PerPixelEffect
{
    public override string Name => "Posterize";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("levels", "Levels", 2, 32, 5),
    };

    protected override PixelMapper CreateMapper(EffectValues values)
    {
        int levels = Math.Max(2, (int)values["levels"]);
        double step = 255.0 / (levels - 1);
        var lut = BuildLut(v => Math.Round(v / step) * step);
        return (ref byte b, ref byte g, ref byte r) =>
        {
            b = lut[b]; g = lut[g]; r = lut[r];
        };
    }
}
