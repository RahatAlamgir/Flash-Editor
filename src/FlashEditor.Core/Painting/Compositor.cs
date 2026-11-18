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
            if (layer.Adjustment is { } adjustment) adjustment.Apply(result, result.Bounds, layer.Opacity);
            else DrawOver(result, layer.Pixels, 0, 0, layer.Opacity, null, layer.BlendMode, null, layer.EffectiveMask);
        }
        return result;
    }

    /// <summary>
    /// Redraws one rectangle of <paramref name="target"/> from the visible layers (with their opacity and blend modes), leaving the rest
    /// alone. The canvas uses this to show blend modes while you work.
    /// </summary>
    public static void FlattenRegion(Document document, PixelRect region, PixelBuffer target)
    {
        region = region.Intersect(target.Bounds);
        if (region.IsEmpty) return;
        target.ClearRegion(region);
        foreach (var layer in document.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0) continue;
            if (layer.Adjustment is { } adjustment) adjustment.Apply(target, region, layer.Opacity);
            else DrawOver(target, layer.Pixels, 0, 0, layer.Opacity, null, layer.BlendMode, region, layer.EffectiveMask);
        }
    }

    /// <summary>Color of the merged visible image at one pixel.</summary>
    public static ColorRgba SampleMerged(Document document, int x, int y)
    {
        if (!document.Bounds.Contains(x, y)) return ColorRgba.Transparent;
        var pixel = new byte[4];
        foreach (var layer in document.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0) continue;
            if (layer.Adjustment is { } adjustment)
            {
                if (pixel[3] != 0) adjustment.Apply(ref pixel[0], ref pixel[1], ref pixel[2], layer.Opacity);
                continue;
            }
            BlendMath.BlendPixel(pixel, 0, layer.Pixels.Data, layer.Pixels.IndexOf(x, y), layer.Opacity * (layer.EffectiveMask?.Coverage(x, y) ?? 1.0), layer.BlendMode);
        }
        return new ColorRgba(pixel[2], pixel[1], pixel[0], pixel[3]);
    }

    /// <summary>
    /// Paints <paramref name="source"/> over <paramref name="target"/> with its top-left at (x, y).
    /// When a selection is given, only selected pixels are affected. Returns the changed rectangle.
    /// </summary>
    public static PixelRect DrawOver(PixelBuffer target, PixelBuffer source, int x, int y,
        double opacity = 1.0, Selection? selection = null, BlendMode mode = BlendMode.Normal, PixelRect? clip = null, LayerMask? mask = null)
    {
        var area = new PixelRect(x, y, source.Width, source.Height).Intersect(target.Bounds);
        if (selection != null) area = area.Intersect(selection.Bounds);
        if (clip is { } limit) area = area.Intersect(limit);
        if (area.IsEmpty || opacity <= 0) return PixelRect.Empty;

        var dst = target.Data;
        var src = source.Data;
        bool plain = selection == null && mask == null && opacity >= 1.0 && mode == BlendMode.Normal;

        void Row(int ty)
        {
            int sy = ty - y;
            for (int tx = area.X; tx < area.Right; tx++)
            {
                int si = source.IndexOf(tx - x, sy);
                byte sourceAlpha = src[si + 3];
                if (sourceAlpha == 0) continue; // nothing to paint

                int di = target.IndexOf(tx, ty);
                if (plain && sourceAlpha == 255)
                {
                    // A solid pixel at full opacity simply replaces what is below (same result as the blend, much cheaper).
                    dst[di] = src[si]; dst[di + 1] = src[si + 1]; dst[di + 2] = src[si + 2]; dst[di + 3] = 255;
                    continue;
                }

                double factor = opacity;
                if (mask != null && tx < mask.Width && ty < mask.Height)
                {
                    factor *= mask.Alpha[ty * mask.Width + tx] / 255.0;
                    if (factor <= 0) continue;
                }
                if (selection != null)
                {
                    factor *= selection.Coverage(tx, ty);
                    if (factor <= 0) continue;
                }
                BlendMath.BlendPixel(dst, di, src, si, factor, mode);
            }
        }

        // Starting threads costs more than a small area (a brush dab, a small object) takes to paint.
        if ((long)area.Width * area.Height < 16384) for (int ty = area.Y; ty < area.Bottom; ty++) Row(ty);
        else Parallel.For(area.Y, area.Bottom, Row);
        return area;
    }

    /// <summary>Paints <paramref name="layer"/>'s content onto <paramref name="target"/> (used by Merge Down).</summary>
    public static void DrawLayer(PixelBuffer target, Layer layer)
    {
        // An adjustment layer changes what is already in the target (Merge Down bakes it into the layer below).
        if (layer.Adjustment is { } adjustment) adjustment.Apply(target, target.Bounds, layer.Opacity);
        else DrawOver(target, layer.Pixels, 0, 0, layer.Opacity, null, layer.BlendMode, null, layer.EffectiveMask);
    }
}
