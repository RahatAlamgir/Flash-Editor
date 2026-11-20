using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.IO;

/// <summary>
/// Writes Windows .ico files. Each icon size is stored as an embedded PNG (supported since Windows Vista).
/// Non-square images are centered on a transparent square so they aren't stretched.
/// </summary>
public static class IcoEncoder
{
    public static readonly int[] DefaultSizes = { 16, 24, 32, 48, 64, 128, 256 };

    public static byte[] Encode(PixelBuffer image, IReadOnlyList<int>? sizes = null)
    {
        sizes ??= DefaultSizes;
        var square = MakeSquare(image);
        var images = sizes
            .Distinct()
            .Where(s => s is > 0 and <= 256)
            .OrderBy(s => s)
            .Select(s => (Size: s, Png: ImageEncoder.EncodePng(Resampler.Resize(square, s, s))))
            .ToList();
        if (images.Count == 0) throw new ArgumentException("No valid icon sizes.", nameof(sizes));
        return Write(images);
    }

    private static byte[] Write(List<(int Size, byte[] Png)> images)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // ICONDIR header
        writer.Write((ushort)0);            // reserved
        writer.Write((ushort)1);            // type: icon
        writer.Write((ushort)images.Count); // number of images

        // ICONDIRENTRY for each image; data follows all entries.
        int offset = 6 + 16 * images.Count;
        foreach (var (size, png) in images)
        {
            writer.Write((byte)(size >= 256 ? 0 : size)); // width (0 means 256)
            writer.Write((byte)(size >= 256 ? 0 : size)); // height
            writer.Write((byte)0);                       // palette colors
            writer.Write((byte)0);                       // reserved
            writer.Write((ushort)1);                     // color planes
            writer.Write((ushort)32);                    // bits per pixel
            writer.Write(png.Length);                    // data size
            writer.Write(offset);                        // data offset
            offset += png.Length;
        }
        foreach (var (_, png) in images) writer.Write(png);

        writer.Flush();
        return stream.ToArray();
    }

    private static PixelBuffer MakeSquare(PixelBuffer image)
    {
        if (image.Width == image.Height) return image;
        int side = Math.Max(image.Width, image.Height);
        var square = new PixelBuffer(side, side);
        square.CopyFrom(image, (side - image.Width) / 2, (side - image.Height) / 2);
        return square;
    }
}
