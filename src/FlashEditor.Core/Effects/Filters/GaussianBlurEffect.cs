using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

public sealed class GaussianBlurEffect : ImageEffect
{
    public override string Name => "Gaussian Blur";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("radius", "Radius", 0.5, 100, 4, 0.5, "px"),
    };

    public override int GetPadding(EffectValues values) => (int)Math.Ceiling(values["radius"] * 3);

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
        => Blur.Gaussian(source, values["radius"], cancellation);
}
