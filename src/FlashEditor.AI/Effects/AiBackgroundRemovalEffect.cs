using FlashEditor.AI.Segmentation;
using FlashEditor.Core.Effects;
using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.Imaging;

namespace FlashEditor.AI.Effects;

/// <summary>
/// Removes the background with an AI model that finds the main subject (person, animal, product...),
/// so it also works on busy backgrounds. The sliders only change how the edge is cut; the model runs
/// once per picture.
/// </summary>
public sealed class AiBackgroundRemovalEffect : ImageEffect
{
    private readonly SubjectSegmenter _segmenter;

    public AiBackgroundRemovalEffect(SubjectSegmenter segmenter) => _segmenter = segmenter;

    public override string Name => "Remove Background (AI)";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("sharpness", "Edge sharpness", 0, 100, 30, 1, "%"),
        new EffectParameter("shift", "Shrink / grow edge", -50, 50, 0, 1),
        new EffectParameter("feather", "Feather", 0, 10, 0, 0.5, "px"),
    };

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        var mask = _segmenter.PredictMask(source, cancellation);
        MaskOps.Adjust(mask, values["sharpness"] / 100.0, values["shift"] / 50.0);
        if (values["feather"] > 0)
            Blur.GaussianInPlace(mask, source.Width, source.Height, 1, values["feather"], cancellation);
        return MaskOps.ApplyAsAlpha(source, mask);
    }
}
