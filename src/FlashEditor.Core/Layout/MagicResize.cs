using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.Layout;

/// <summary>
/// Magic Resize: turns a design into another canvas size (a post into a story, a poster into a banner) and keeps the layout. Everything keeps
/// its proportions and moves to the same relative place; a background that covered the whole canvas covers the new one.
/// </summary>
public static class MagicResize
{
    /// <summary>An object is a "background" when it covers (nearly) the whole old canvas.</summary>
    public static bool IsBackground(LayerObject obj, int oldWidth, int oldHeight)
    {
        var b = obj.Box;
        return Math.Abs(b.Rotation) < 0.5 && b.Width >= oldWidth * 0.9 && b.Height >= oldHeight * 0.9;
    }

    /// <summary>The object on the new canvas.</summary>
    public static LayerObject MapObject(LayerObject obj, int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        double sx = newWidth / (double)oldWidth, sy = newHeight / (double)oldHeight, s = Math.Min(sx, sy);

        if (IsBackground(obj, oldWidth, oldHeight))
        {
            // A flat shape stretches to the new canvas. Pictures and everything else cover it without being distorted.
            if (obj is ShapeObject)
                return obj with { Box = new ObjectBox(0, 0, newWidth, newHeight, 0) };
            double cover = Math.Max(newWidth / obj.Box.Width, newHeight / obj.Box.Height);
            double w = obj.Box.Width * cover, h = obj.Box.Height * cover;
            var scaledBackground = ObjectTransforms.Scale(obj with { Box = obj.Box.WithCenter(new PointD(oldWidth / 2.0, oldHeight / 2.0)) }, cover, cover);
            return ObjectTransforms.WithBox(scaledBackground, ObjectBox.FromCenter(new PointD(newWidth / 2.0, newHeight / 2.0), w, h, 0));
        }

        // Size by the smaller change, place by the relative position of the middle.
        var scaled = ObjectTransforms.Scale(obj, s, s);
        var center = obj.Box.Center;
        return ObjectTransforms.Offset(scaled, center.X * (sx - s), center.Y * (sy - s));
    }

    /// <summary>A layer of plain pixels on the new canvas (what is painted keeps its proportions; a layer that is solid everywhere covers the canvas).</summary>
    public static PixelBuffer MapPixels(PixelBuffer pixels, int newWidth, int newHeight)
    {
        int oldWidth = pixels.Width, oldHeight = pixels.Height;
        double sx = newWidth / (double)oldWidth, sy = newHeight / (double)oldHeight, s = Math.Min(sx, sy);
        var result = new PixelBuffer(newWidth, newHeight);

        var bounds = pixels.VisibleBounds();
        if (bounds.IsEmpty) return result;

        long solid = 0;
        for (int i = 3; i < pixels.Data.Length; i += 4) if (pixels.Data[i] > 250) solid++;
        bool covers = solid >= pixels.Data.Length / 4 * 0.95;

        if (covers)
        {
            double cover = Math.Max(sx, sy);
            int w = Math.Max(newWidth, (int)Math.Round(oldWidth * cover)), h = Math.Max(newHeight, (int)Math.Round(oldHeight * cover));
            var big = Resampler.Resize(pixels, w, h, ResampleMode.Smooth);
            result.CopyFrom(big, new PixelRect((w - newWidth) / 2, (h - newHeight) / 2, newWidth, newHeight), 0, 0);
            return result;
        }

        var content = pixels.Crop(bounds);
        int cw = Math.Max(1, (int)Math.Round(content.Width * s)), ch = Math.Max(1, (int)Math.Round(content.Height * s));
        var scaled = Resampler.Resize(content, cw, ch, ResampleMode.Smooth);
        double cx = (bounds.X + bounds.Width / 2.0) * sx, cy = (bounds.Y + bounds.Height / 2.0) * sy;
        Compositor.DrawOver(result, scaled, (int)Math.Round(cx - cw / 2.0), (int)Math.Round(cy - ch / 2.0));
        return result;
    }
}
