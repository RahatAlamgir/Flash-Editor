using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO;

/// <summary>
/// Writes a Windows bitmap (.bmp) with 32 bits per pixel and a real alpha channel (BMP version 4 header),
/// so transparency survives. Rows are stored bottom to top, as the format expects.
/// </summary>
public static class BmpEncoder
{
    private const int FileHeaderSize = 14, InfoHeaderSize = 108;

    public static byte[] Encode(PixelBuffer image)
    {
        int w = image.Width, h = image.Height;
        int pixelBytes = w * h * 4;
        int offset = FileHeaderSize + InfoHeaderSize;
        var bytes = new byte[offset + pixelBytes];

        void Put32(int at, uint value) { bytes[at] = (byte)value; bytes[at + 1] = (byte)(value >> 8); bytes[at + 2] = (byte)(value >> 16); bytes[at + 3] = (byte)(value >> 24); }
        void Put16(int at, ushort value) { bytes[at] = (byte)value; bytes[at + 1] = (byte)(value >> 8); }

        // File header
        bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        Put32(2, (uint)bytes.Length);
        Put32(10, (uint)offset);

        // BITMAPV4HEADER
        int p = FileHeaderSize;
        Put32(p, InfoHeaderSize);
        Put32(p + 4, (uint)w);
        Put32(p + 8, (uint)h);                  // positive height = bottom-up rows
        Put16(p + 12, 1);                       // planes
        Put16(p + 14, 32);                      // bits per pixel
        Put32(p + 16, 3);                       // BI_BITFIELDS: the masks below say where each color sits
        Put32(p + 20, (uint)pixelBytes);
        Put32(p + 24, 2835); Put32(p + 28, 2835); // 72 dpi
        Put32(p + 40, 0x00FF0000);              // red mask
        Put32(p + 44, 0x0000FF00);              // green mask
        Put32(p + 48, 0x000000FF);              // blue mask
        Put32(p + 52, 0xFF000000);              // alpha mask
        Put32(p + 56, 0x73524742);              // color space: sRGB

        // Pixels: our buffer is already B, G, R, A per pixel; only the row order is reversed.
        int stride = image.Stride;
        for (int y = 0; y < h; y++)
            Buffer.BlockCopy(image.Data, (h - 1 - y) * stride, bytes, offset + y * stride, stride);
        return bytes;
    }
}
