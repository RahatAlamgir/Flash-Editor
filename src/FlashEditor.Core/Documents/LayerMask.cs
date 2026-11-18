using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Documents;

/// <summary>
/// A layer mask: one value per canvas pixel that says how much of the layer shows there. 255 = fully visible, 0 = hidden, in between = partly
/// see-through. Painting on the mask hides or reveals parts of a layer without erasing its pixels, so it can always be changed back.
/// </summary>
public sealed class LayerMask
{
    public LayerMask(int width, int height, byte fill = 255)
    {
        Width = width;
        Height = height;
        Alpha = new byte[width * height];
        if (fill != 0) Array.Fill(Alpha, fill);
    }

    private LayerMask(int width, int height, byte[] alpha)
    {
        Width = width;
        Height = height;
        Alpha = alpha;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>One byte per pixel, row by row (the buffer is live).</summary>
    public byte[] Alpha { get; }

    public PixelRect Bounds => new(0, 0, Width, Height);

    /// <summary>Memory used by the mask, in bytes.</summary>
    public long ByteSize => Alpha.Length;

    public LayerMask Clone() => new(Width, Height, (byte[])Alpha.Clone());

    /// <summary>How much shows at a pixel, 0..1.</summary>
    public double Coverage(int x, int y) => Alpha[y * Width + x] / 255.0;

    /// <summary>True when every pixel shows completely (the mask changes nothing).</summary>
    public bool IsAllVisible => Alpha.AsSpan().IndexOfAnyExcept((byte)255) < 0;

    /// <summary>Makes what shows hidden and the other way round.</summary>
    public void Invert()
    {
        for (int i = 0; i < Alpha.Length; i++) Alpha[i] = (byte)(255 - Alpha[i]);
    }

    /// <summary>A mask that shows only the selected area (<paramref name="revealSelected"/>) or everything except the selected area.</summary>
    public static LayerMask FromSelection(Selection selection, bool revealSelected)
    {
        var mask = new LayerMask(selection.CanvasWidth, selection.CanvasHeight, 0);
        for (int i = 0; i < mask.Alpha.Length; i++)
            mask.Alpha[i] = revealSelected ? selection.Mask[i] : (byte)(255 - selection.Mask[i]);
        return mask;
    }

    /// <summary>Hides (or reveals) the selected area on top of what the mask already says.</summary>
    public void ApplySelection(Selection selection, bool reveal)
    {
        var area = selection.Bounds.Intersect(Bounds);
        for (int y = area.Y; y < area.Bottom; y++)
            for (int x = area.X; x < area.Right; x++)
            {
                int i = y * Width + x;
                int s = selection.Mask[i];
                if (s == 0) continue;
                // reveal: move the mask value towards 255 by the selection's coverage; hide: towards 0
                Alpha[i] = reveal ? (byte)(Alpha[i] + (255 - Alpha[i]) * s / 255) : (byte)(Alpha[i] - Alpha[i] * s / 255);
            }
    }

    /// <summary>The mask values inside a rectangle (for undo).</summary>
    public byte[] CopyRegion(PixelRect region)
    {
        region = region.Intersect(Bounds);
        var data = new byte[Math.Max(0, region.Width) * Math.Max(0, region.Height)];
        for (int y = 0; y < region.Height; y++)
            Array.Copy(Alpha, (region.Y + y) * Width + region.X, data, y * region.Width, region.Width);
        return data;
    }

    /// <summary>Puts values saved by <see cref="CopyRegion"/> back.</summary>
    public void PasteRegion(PixelRect region, byte[] data)
    {
        region = region.Intersect(Bounds);
        for (int y = 0; y < region.Height; y++)
            Array.Copy(data, y * region.Width, Alpha, (region.Y + y) * Width + region.X, region.Width);
    }

    /// <summary>The mask as a grey picture (white = shows, black = hidden): for the thumbnail and for saving.</summary>
    public PixelBuffer ToGrayPicture()
    {
        var picture = new PixelBuffer(Width, Height);
        var d = picture.Data;
        for (int p = 0; p < Alpha.Length; p++)
        {
            byte v = Alpha[p];
            d[p * 4] = v; d[p * 4 + 1] = v; d[p * 4 + 2] = v; d[p * 4 + 3] = 255;
        }
        return picture;
    }

    /// <summary>A mask from a grey picture made by <see cref="ToGrayPicture"/>.</summary>
    public static LayerMask FromGrayPicture(PixelBuffer picture)
    {
        var mask = new LayerMask(picture.Width, picture.Height, 0);
        for (int p = 0; p < mask.Alpha.Length; p++) mask.Alpha[p] = picture.Data[p * 4 + 1];
        return mask;
    }

    /// <summary>A copy of <paramref name="source"/> in which the pixels are as see-through as the mask says (the mask "applied").</summary>
    public static PixelBuffer Bake(PixelBuffer source, LayerMask mask, int offsetX = 0, int offsetY = 0)
    {
        var result = source.Clone();
        var d = result.Data;
        for (int y = 0; y < mask.Height; y++)
        {
            int sy = y - offsetY;
            if (sy < 0 || sy >= result.Height) continue;
            for (int x = 0; x < mask.Width; x++)
            {
                int sx = x - offsetX;
                if (sx < 0 || sx >= result.Width) continue;
                byte m = mask.Alpha[y * mask.Width + x];
                if (m == 255) continue;
                int i = (sy * result.Width + sx) * 4 + 3;
                d[i] = (byte)(d[i] * m / 255);
            }
        }
        return result;
    }
}
