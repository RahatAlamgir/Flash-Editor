using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Painting;

/// <summary>Combines pixel buffers and layers ("normal" blend mode).</summary>
public static class Compositor
{
    /// <summary>Merges all visible layers into a single image (used for export and the eyedropper).</summary>
    public static PixelBuffer Flatten(Document document)
    {
        var result = new PixelBuffer(document.Width, document.Height);
        foreach (var layer in document.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0) continue;
            DrawOver(result, layer.Pixels, 0, 0, layer.Opacity);
        }
        return result;
    }

    /// <summary>Color of the merged visible image at one pixel.</summary>
    public static ColorRgba SampleMerged(Document document, int x, int y)
    {
        if (!document.Bounds.Contains(x, y)) return ColorRgba.Transparent;
        var pixel = new byte[4];
        foreach (var layer in document.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0) continue;
            BlendMath.OverPixel(pixel, 0, layer.Pixels.Data, layer.Pixels.IndexOf(x, y), layer.Opacity);
        }
        return new ColorRgba(pixel[2], pixel[1], pixel[0], pixel[3]);
    }

    /// <summary>
    /// Paints <paramref name="source"/> over <paramref name="target"/> with its top-left at (x, y).
    /// When a selection is given, only selected pixels are affected. Returns the changed rectangle.
    /// </summary>
    public static PixelRect DrawOver(PixelBuffer target, PixelBuffer source, int x, int y,
        double opacity = 1.0, Selection? selection = null)
    {
        var area = new PixelRect(x, y, source.Width, source.Height).Intersect(target.Bounds);
        if (selection != null) area = area.Intersect(selection.Bounds);
        if (area.IsEmpty || opacity <= 0) return PixelRect.Empty;

        Parallel.For(area.Y, area.Bottom, ty =>
        {
            int sy = ty - y;
            for (int tx = area.X; tx < area.Right; tx++)
            {
                double factor = opacity;
                if (selection != null)
                {
                    factor *= selection.Coverage(tx, ty);
                    if (factor <= 0) continue;
                }
                BlendMath.OverPixel(target.Data, target.IndexOf(tx, ty), source.Data, source.IndexOf(tx - x, sy), factor);
            }
        });
        return area;
    }

    /// <summary>Paints <paramref name="layer"/>'s content onto <paramref name="target"/> (used by Merge Down).</summary>
    public static void DrawLayer(PixelBuffer target, Layer layer)
        => DrawOver(target, layer.Pixels, 0, 0, layer.Opacity);
}
