using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects;

/// <summary>
/// Base class for every adjustment and filter. An effect takes a source image and returns a new,
/// same-sized image; it never modifies the source.
/// </summary>
public abstract class ImageEffect
{
    /// <summary>Menu / dialog / history name, e.g. "Gaussian Blur".</summary>
    public abstract string Name { get; }

    /// <summary>Settings shown in the effect dialog. Empty = the effect is applied immediately.</summary>
    public virtual IReadOnlyList<EffectParameter> Parameters => Array.Empty<EffectParameter>();

    public bool HasParameters => Parameters.Count > 0;

    /// <summary>
    /// How many pixels outside a selection this effect reads (e.g. blur radius).
    /// Used to process only the selected part of large images.
    /// </summary>
    public virtual int GetPadding(EffectValues values) => 0;

    public EffectValues CreateDefaultValues() => new(Parameters);

    public abstract PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default);

    public override string ToString() => Name;
}
