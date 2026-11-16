namespace FlashEditor.Core.Imaging;

/// <summary>
/// A block of 32-bit pixels stored as B,G,R,A bytes with straight (non-premultiplied) alpha.
/// This is the pixel storage used by every layer, effect and file codec in Flash Editor.
/// </summary>
public sealed class PixelBuffer
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Data { get; }

    /// <summary>Bytes per row.</summary>
    public int Stride => Width * 4;
    public PixelRect Bounds => new(0, 0, Width, Height);

    public PixelBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), $"Invalid image size {width}x{height}.");
        Width = width;
        Height = height;
        Data = new byte[checked(width * height * 4)];
    }

    public PixelBuffer(int width, int height, byte[] data) : this(width, height, data, validateOnly: true) { }

    private PixelBuffer(int width, int height, byte[] data, bool validateOnly)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), $"Invalid image size {width}x{height}.");
        if (data.Length != width * height * 4)
            throw new ArgumentException("Pixel data length does not match the size.", nameof(data));
        Width = width;
        Height = height;
        Data = data;
    }

    public int IndexOf(int x, int y) => (y * Width + x) * 4;

    public ColorRgba GetPixel(int x, int y)
    {
        int i = IndexOf(x, y);
        return new ColorRgba(Data[i + 2], Data[i + 1], Data[i], Data[i + 3]);
    }

    public void SetPixel(int x, int y, ColorRgba c)
    {
        int i = IndexOf(x, y);
        Data[i] = c.B; Data[i + 1] = c.G; Data[i + 2] = c.R; Data[i + 3] = c.A;
    }

    public PixelBuffer Clone() => new(Width, Height, (byte[])Data.Clone(), validateOnly: true);

    public void Clear() => Array.Clear(Data);

    public void Fill(ColorRgba c)
    {
        if (Data.Length == 0) return;
        Data[0] = c.B; Data[1] = c.G; Data[2] = c.R; Data[3] = c.A;
        // Doubling copy: fast fill without a per-pixel loop.
        int filled = 4;
        while (filled < Data.Length)
        {
            int count = Math.Min(filled, Data.Length - filled);
            Buffer.BlockCopy(Data, 0, Data, filled, count);
            filled += count;
        }
    }

    /// <summary>
    /// Returns a new buffer containing <paramref name="rect"/>. Parts outside this buffer become transparent.
    /// </summary>
    public PixelBuffer Crop(PixelRect rect)
    {
        var result = new PixelBuffer(rect.Width, rect.Height);
        result.CopyFrom(this, rect, 0, 0);
        return result;
    }

    /// <summary>
    /// Copies raw pixels (no blending) from <paramref name="source"/>'s <paramref name="sourceRect"/>
    /// to this buffer at (<paramref name="destX"/>, <paramref name="destY"/>). Everything is clipped.
    /// </summary>
    public void CopyFrom(PixelBuffer source, PixelRect sourceRect, int destX, int destY)
    {
        // Clip against the source.
        var src = sourceRect.Intersect(source.Bounds);
        if (src.IsEmpty) return;
        destX += src.X - sourceRect.X;
        destY += src.Y - sourceRect.Y;

        // Clip against the destination.
        var dst = new PixelRect(destX, destY, src.Width, src.Height).Intersect(Bounds);
        if (dst.IsEmpty) return;
        int sx = src.X + (dst.X - destX);
        int sy = src.Y + (dst.Y - destY);

        int rowBytes = dst.Width * 4;
        for (int row = 0; row < dst.Height; row++)
        {
            Buffer.BlockCopy(source.Data, source.IndexOf(sx, sy + row), Data, IndexOf(dst.X, dst.Y + row), rowBytes);
        }
    }

    /// <summary>Copies the whole of <paramref name="source"/> to (x, y), clipped.</summary>
    public void CopyFrom(PixelBuffer source, int destX, int destY)
        => CopyFrom(source, source.Bounds, destX, destY);

    /// <summary>
    /// The smallest rectangle containing every pixel that is not fully transparent.
    /// Empty when the whole buffer is transparent.
    /// </summary>
    public PixelRect VisibleBounds()
    {
        int minX = Width, minY = Height, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
        {
            int row = y * Stride;
            int first = -1, last = -1;
            for (int x = 0; x < Width; x++)
            {
                if (Data[row + x * 4 + 3] == 0) continue;
                if (first < 0) first = x;
                last = x;
            }
            if (first < 0) continue;
            if (first < minX) minX = first;
            if (last > maxX) maxX = last;
            if (y < minY) minY = y;
            maxY = y;
        }
        return maxX < 0 ? PixelRect.Empty : new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>True if both buffers have the same size and identical bytes.</summary>
    public bool ContentEquals(PixelBuffer other)
        => other.Width == Width && other.Height == Height && Data.AsSpan().SequenceEqual(other.Data);
}
