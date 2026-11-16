using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Adjustments;

public sealed class GrayscaleEffect : PerPixelEffect
{
    public override string Name => "Grayscale";

    protected override PixelMapper CreateMapper(EffectValues values)
        => (ref byte b, ref byte g, ref byte r) =>
        {
            // Perceptual weights: green looks brightest to the eye, blue darkest.
            byte gray = ColorMath.ClampByte(0.299 * r + 0.587 * g + 0.114 * b);
            r = g = b = gray;
        };
}
