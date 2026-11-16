namespace FlashEditor.Core.Effects.Adjustments;

/// <summary>Pure black and white: pixels brighter than the level become white, the rest black.</summary>
public sealed class ThresholdEffect : PerPixelEffect
{
    public override string Name => "Threshold";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("level", "Level", 1, 255, 128),
    };

    protected override PixelMapper CreateMapper(EffectValues values)
    {
        double level = values["level"];
        return (ref byte b, ref byte g, ref byte r) =>
        {
            byte v = 0.299 * r + 0.587 * g + 0.114 * b >= level ? (byte)255 : (byte)0;
            r = g = b = v;
        };
    }
}
