using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Documents;

/// <summary>
/// Everything that makes up a layer's image: the canvas-sized <see cref="Pixels"/> plus, when part of the
/// layer was moved past the canvas edge, a larger <see cref="Extended"/> buffer that also keeps the hidden
/// parts. <see cref="ExtendedX"/>/<see cref="ExtendedY"/> is where that buffer's top-left corner sits on the
/// canvas (zero or negative). The extended buffer always covers the whole canvas.
/// <see cref="Object"/> is set for object layers (shape, text, image); their pixels are its rendering.
/// </summary>
public sealed record LayerState(PixelBuffer Pixels, PixelBuffer? Extended, int ExtendedX, int ExtendedY, LayerObject? Object = null,
    LayerMask? Mask = null, bool IncludesMask = false)
{
    /// <summary>A layer state with no hidden pixels.</summary>
    public static LayerState Simple(PixelBuffer pixels) => new(pixels, null, 0, 0);

    /// <summary>Memory used by the pixel buffers of this state, in bytes.</summary>
    public long ByteSize => (long)Pixels.Data.Length + (Extended?.Data.Length ?? 0) + (Mask?.ByteSize ?? 0);

    /// <summary>Deep copy (new buffers).</summary>
    public LayerState DeepCopy() => new(Pixels.Clone(), Extended?.Clone(), ExtendedX, ExtendedY, Object, Mask?.Clone(), IncludesMask);

    /// <summary>
    /// Builds the state for a canvas of <paramref name="canvasWidth"/> x <paramref name="canvasHeight"/>
    /// when <paramref name="content"/> is placed with its top-left corner at (<paramref name="x"/>,
    /// <paramref name="y"/>). Parts that fall outside the canvas are kept in the extended buffer;
    /// fully transparent margins are dropped so the buffer never grows without reason.
    /// </summary>
    public static LayerState Place(PixelBuffer content, int x, int y, int canvasWidth, int canvasHeight)
    {
        var pixels = new PixelBuffer(canvasWidth, canvasHeight);
        pixels.CopyFrom(content, x, y);

        var canvas = new PixelRect(0, 0, canvasWidth, canvasHeight);
        var visible = content.VisibleBounds().Offset(x, y);
        if (visible.IsEmpty || visible.Intersect(canvas) == visible)
            return Simple(pixels);

        var keep = canvas.Union(visible);
        var extended = new PixelBuffer(keep.Width, keep.Height);
        extended.CopyFrom(content, x - keep.X, y - keep.Y);
        return new LayerState(pixels, extended, keep.X, keep.Y);
    }
}
