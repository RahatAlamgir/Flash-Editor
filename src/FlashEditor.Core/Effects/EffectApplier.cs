using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Effects;

/// <summary>Runs an effect on a layer's pixels, restricted to the selection when there is one.</summary>
public static class EffectApplier
{
    public static PixelBuffer Apply(PixelBuffer source, ImageEffect effect, EffectValues values,
        Selection? selection, CancellationToken cancellation = default)
    {
        if (selection == null)
            return effect.Apply(source, values, cancellation);

        // Only process the selected area (plus the margin the effect needs to read).
        var bounds = selection.Bounds.Intersect(source.Bounds);
        if (bounds.IsEmpty) return source.Clone();

        var region = bounds.Inflate(effect.GetPadding(values)).Intersect(source.Bounds);
        var processed = effect.Apply(source.Crop(region), values, cancellation);

        // Blend effect result into a copy of the original using the selection mask.
        var result = source.Clone();
        Parallel.For(bounds.Y, bounds.Bottom, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            for (int x = bounds.X; x < bounds.Right; x++)
            {
                double coverage = selection.Coverage(x, y);
                if (coverage <= 0) continue;
                int di = result.IndexOf(x, y);
                int pi = processed.IndexOf(x - region.X, y - region.Y);
                BlendMath.Lerp(result.Data, di, source.Data, di, processed.Data, pi, coverage);
            }
        });
        return result;
    }
}
