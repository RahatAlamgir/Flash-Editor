using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Documents;

/// <summary>
/// Crops a single layer (a picture, object or painted layer) instead of the whole canvas.
/// The result is an image object, so the cropped piece can still be moved and resized cleanly.
/// </summary>
public static class LayerCrop
{
    /// <summary>The layer cut down to <paramref name="rect"/> (canvas coordinates).</summary>
    public static LayerState ToRectangle(Layer layer, PixelRect rect)
    {
        int w = layer.Width, h = layer.Height;

        // An upright, unflipped picture is cropped from its original pixels: no quality loss.
        if (layer.Object is ImageObject { Box.Rotation: 0, FlipX: false, FlipY: false } image)
        {
            var b = image.Box;
            double left = Math.Max(rect.X, b.X), top = Math.Max(rect.Y, b.Y);
            double right = Math.Min(rect.Right, b.X + b.Width), bottom = Math.Min(rect.Bottom, b.Y + b.Height);
            if (right - left < 1 || bottom - top < 1) return LayerState.Simple(new PixelBuffer(w, h));

            double sx = image.Source.Width / b.Width, sy = image.Source.Height / b.Height;
            var sourceRect = PixelRect.FromBounds((left - b.X) * sx, (top - b.Y) * sy, (right - b.X) * sx, (bottom - b.Y) * sy)
                .Intersect(image.Source.Bounds);
            if (sourceRect.IsEmpty) return LayerState.Simple(new PixelBuffer(w, h));
            var cropped = image with
            {
                Source = image.Source.Crop(sourceRect),
                Box = new ObjectBox(left, top, right - left, bottom - top),
            };
            return Layer.RenderState(cropped, w, h);
        }

        // Anything else: take what the layer shows (including parts outside the canvas) inside the rectangle.
        var (content, cx, cy) = layer.GetFullContent();
        var piece = content.Crop(rect.Offset(-cx, -cy));
        return AsImage(piece, rect, layer.Name, w, h);
    }

    /// <summary>The layer keeping only the selected pixels (any selection shape, soft edges kept).</summary>
    public static LayerState ToSelection(Layer layer, Selection selection)
    {
        var bounds = selection.Bounds.Intersect(layer.Pixels.Bounds);
        if (bounds.IsEmpty) return LayerState.Simple(new PixelBuffer(layer.Width, layer.Height));
        var piece = layer.Pixels.Crop(bounds);
        for (int y = 0; y < bounds.Height; y++)
        {
            for (int x = 0; x < bounds.Width; x++)
            {
                int i = piece.IndexOf(x, y);
                piece.Data[i + 3] = ColorMath.ClampByte(piece.Data[i + 3] * selection.Coverage(bounds.X + x, bounds.Y + y));
            }
        }
        return AsImage(piece, bounds, layer.Name, layer.Width, layer.Height);
    }

    /// <summary>The piece as a resizable image object, trimmed to its visible pixels.</summary>
    private static LayerState AsImage(PixelBuffer piece, PixelRect at, string name, int w, int h)
    {
        var visible = piece.VisibleBounds();
        if (visible.IsEmpty) return LayerState.Simple(new PixelBuffer(w, h));
        var trimmed = piece.Crop(visible);
        var box = new ObjectBox(at.X + visible.X, at.Y + visible.Y, visible.Width, visible.Height);
        return Layer.RenderState(new ImageObject(box, trimmed, name), w, h);
    }
}
