using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Adjustments;

public sealed class SepiaEffect : PerPixelEffect
{
    public override string Name => "Sepia";

    protected override PixelMapper CreateMapper(EffectValues values)
        => (ref byte b, ref byte g, ref byte r) =>
        {
            // Standard sepia tone matrix.
            double nr = 0.393 * r + 0.769 * g + 0.189 * b;
            double ng = 0.349 * r + 0.686 * g + 0.168 * b;
            double nb = 0.272 * r + 0.534 * g + 0.131 * b;
            r = ColorMath.ClampByte(nr);
            g = ColorMath.ClampByte(ng);
            b = ColorMath.ClampByte(nb);
        };
}
