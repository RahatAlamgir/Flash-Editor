using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// A placed picture that can be moved, resized and rotated without losing quality: it is always
/// redrawn from the original <see cref="Source"/> pixels.
/// </summary>
public sealed record ImageObject(
    ObjectBox Box,
    PixelBuffer Source,
    string Name = "Image",
    bool FlipX = false,
    bool FlipY = false) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Image";
    public override string SuggestedLayerName => Name;

    /// <summary>Places <paramref name="source"/> centered on the canvas, scaled down to fit when it is larger.</summary>
    public static ImageObject CenteredOn(PixelBuffer source, int canvasWidth, int canvasHeight, string name, double maxFill = 1.0)
    {
        double scale = Math.Min(1.0, Math.Min(canvasWidth * maxFill / source.Width, canvasHeight * maxFill / source.Height));
        double w = Math.Max(1, source.Width * scale), h = Math.Max(1, source.Height * scale);
        return new ImageObject(new ObjectBox((canvasWidth - w) / 2, (canvasHeight - h) / 2, w, h), source, name);
    }
}
