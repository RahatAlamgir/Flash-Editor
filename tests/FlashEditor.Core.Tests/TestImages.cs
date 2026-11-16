using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests;

/// <summary>Helpers that build small images for tests.</summary>
internal static class TestImages
{
    public static PixelBuffer Solid(int w, int h, ColorRgba color)
    {
        var buffer = new PixelBuffer(w, h);
        buffer.Fill(color);
        return buffer;
    }

    /// <summary>Each pixel gets a unique color derived from its position (easy to track after transforms).</summary>
    public static PixelBuffer Numbered(int w, int h)
    {
        var buffer = new PixelBuffer(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                buffer.SetPixel(x, y, new ColorRgba((byte)x, (byte)y, (byte)(x + y * w)));
        return buffer;
    }

    /// <summary>White background with a colored square in the middle.</summary>
    public static PixelBuffer SubjectOnWhite(int size, int squareSize, ColorRgba squareColor)
    {
        var buffer = Solid(size, size, ColorRgba.White);
        int start = (size - squareSize) / 2;
        for (int y = start; y < start + squareSize; y++)
            for (int x = start; x < start + squareSize; x++)
                buffer.SetPixel(x, y, squareColor);
        return buffer;
    }
}
