using System.Runtime.InteropServices;
using FlashEditor.Core.Imaging;
using SkiaSharp;

namespace FlashEditor.Core.IO;

/// <summary>Writes a <see cref="PixelBuffer"/> as PNG, JPEG, WebP or ICO.</summary>
public static class ImageEncoder
{
    /// <param name="quality">1..100, used by JPEG and WebP (100 WebP = lossless).</param>
    /// <param name="matte">Color behind transparent pixels for formats without transparency (JPEG).</param>
    public static void Save(PixelBuffer image, string path, ImageFileFormat format, int quality = 90, ColorRgba? matte = null)
    {
        byte[] bytes = Encode(image, format, quality, matte);
        // Write to a temp file first so a failure never destroys an existing file.
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    public static byte[] Encode(PixelBuffer image, ImageFileFormat format, int quality = 90, ColorRgba? matte = null)
    {
        quality = Math.Clamp(quality, 1, 100);
        return format switch
        {
            ImageFileFormat.Png => EncodeWithSkia(image, SKEncodedImageFormat.Png, 100),
            ImageFileFormat.Jpeg => EncodeWithSkia(FlattenOnto(image, matte ?? ColorRgba.White), SKEncodedImageFormat.Jpeg, quality),
            ImageFileFormat.Webp => EncodeWithSkia(image, SKEncodedImageFormat.Webp, quality),
            ImageFileFormat.Ico => IcoEncoder.Encode(image),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    public static byte[] EncodePng(PixelBuffer image) => EncodeWithSkia(image, SKEncodedImageFormat.Png, 100);

    private static byte[] EncodeWithSkia(PixelBuffer image, SKEncodedImageFormat format, int quality)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), image.Stride);
            using var data = pixmap.Encode(format, quality)
                ?? throw new InvalidOperationException($"Could not encode the image as {format}.");
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Removes transparency by painting the image over a solid color.</summary>
    private static PixelBuffer FlattenOnto(PixelBuffer image, ColorRgba matte)
    {
        var result = new PixelBuffer(image.Width, image.Height);
        result.Fill(matte with { A = 255 });
        for (int i = 0; i < image.Data.Length; i += 4)
            BlendMath.OverPixel(result.Data, i, image.Data, i, 1.0);
        return result;
    }
}
