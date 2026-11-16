using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Painting;

/// <summary>Copy / clear helpers for the pixels inside a selection.</summary>
public static class SelectionPixels
{
    /// <summary>
    /// Returns the selected pixels cropped to the selection bounds. Pixels outside the selection
    /// become transparent; partially selected pixels keep partial alpha.
    /// </summary>
    public static PixelBuffer Extract(PixelBuffer source, Selection selection)
    {
        var bounds = selection.Bounds;
        var result = source.Crop(bounds);
        for (int y = 0; y < bounds.Height; y++)
        {
            for (int x = 0; x < bounds.Width; x++)
            {
                double coverage = selection.Coverage(bounds.X + x, bounds.Y + y);
                if (coverage >= 1) continue;
                int i = result.IndexOf(x, y);
                result.Data[i + 3] = ColorMath.ClampByte(result.Data[i + 3] * coverage);
            }
        }
        return result;
    }

    /// <summary>Makes the selected pixels transparent. Returns the changed rectangle.</summary>
    public static PixelRect Clear(PixelBuffer target, Selection selection)
    {
        var bounds = selection.Bounds.Intersect(target.Bounds);
        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (int x = bounds.X; x < bounds.Right; x++)
            {
                double coverage = selection.Coverage(x, y);
                if (coverage <= 0) continue;
                int i = target.IndexOf(x, y);
                target.Data[i + 3] = ColorMath.ClampByte(target.Data[i + 3] * (1 - coverage));
            }
        }
        return bounds;
    }

    /// <summary>Fills the selection with a color. Returns the changed rectangle.</summary>
    public static PixelRect Fill(PixelBuffer target, Selection selection, ColorRgba color)
    {
        var bounds = selection.Bounds.Intersect(target.Bounds);
        for (int y = bounds.Y; y < bounds.Bottom; y++)
            for (int x = bounds.X; x < bounds.Right; x++)
                BlendMath.Over(target.Data, target.IndexOf(x, y), color.B, color.G, color.R,
                    color.A / 255.0 * selection.Coverage(x, y));
        return bounds;
    }
}
