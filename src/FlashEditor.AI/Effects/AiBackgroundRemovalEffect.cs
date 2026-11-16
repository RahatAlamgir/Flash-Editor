using FlashEditor.AI.Segmentation;
using FlashEditor.Core.Effects;
using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.Imaging;

namespace FlashEditor.AI.Effects;

/// <summary>
/// Removes the background with an AI model that finds the main subject (person, animal, product...),
/// so it also works on busy backgrounds. The sliders only change how the edge is cut; the model runs
/// once per picture.
/// <para>
/// The model only looks at the part of the layer that has pixels. A small photo placed on a big empty
/// canvas would otherwise be squeezed into a corner of what the model sees, next to a large black
/// (transparent) area, and parts of the subject touching that area (hands, legs) get lost.
/// </para>
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
        var area = source.VisibleBounds();
        if (area.IsEmpty) return source.Clone();

        // What the model sees: only the visible area, with see-through pixels shown on white.
        var piece = OnWhite(source.Crop(area));
        var pieceMask = _segmenter.PredictMask(piece, cancellation);
        MaskOps.Adjust(pieceMask, values["sharpness"] / 100.0, values["shift"] / 50.0);
        if (values["feather"] > 0)
            Blur.GaussianInPlace(pieceMask, area.Width, area.Height, 1, values["feather"], cancellation);

        // Put the mask back in place; outside the visible area everything is already transparent.
        var mask = new float[source.Width * source.Height];
        for (int y = 0; y < area.Height; y++)
            Array.Copy(pieceMask, y * area.Width, mask, (area.Y + y) * source.Width + area.X, area.Width);
        return MaskOps.ApplyAsAlpha(source, mask);
    }

    /// <summary>A copy with transparent parts filled with white (the model only understands solid colors).</summary>
    private static PixelBuffer OnWhite(PixelBuffer image)
    {
        var result = image.Clone();
        var d = result.Data;
        for (int i = 0; i < d.Length; i += 4)
        {
            double a = d[i + 3] / 255.0;
            if (a >= 1) continue;
            d[i] = ColorMath.ClampByte(d[i] * a + 255 * (1 - a));
            d[i + 1] = ColorMath.ClampByte(d[i + 1] * a + 255 * (1 - a));
            d[i + 2] = ColorMath.ClampByte(d[i + 2] * a + 255 * (1 - a));
            d[i + 3] = 255;
        }
        return result;
    }
}
