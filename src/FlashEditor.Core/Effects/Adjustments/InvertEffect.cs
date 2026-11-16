namespace FlashEditor.Core.Effects.Adjustments;

public sealed class InvertEffect : PerPixelEffect
{
    public override string Name => "Invert";

    protected override PixelMapper CreateMapper(EffectValues values)
        => (ref byte b, ref byte g, ref byte r) =>
        {
            b = (byte)(255 - b);
            g = (byte)(255 - g);
            r = (byte)(255 - r);
        };
}
