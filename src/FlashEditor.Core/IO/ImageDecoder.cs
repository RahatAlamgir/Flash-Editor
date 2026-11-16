using System.Runtime.InteropServices;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;
using SkiaSharp;

namespace FlashEditor.Core.IO;

/// <summary>Reads PNG, JPEG, WebP, ICO, BMP and GIF files into a <see cref="PixelBuffer"/> (via SkiaSharp).</summary>
public static class ImageDecoder
{
    /// <summary>Largest side we accept, to avoid running out of memory on giant images.</summary>
    public const int MaxDimension = 16384;

    public static PixelBuffer Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static PixelBuffer Load(Stream stream)
    {
        // SKCodec needs a seekable stream for some formats.
        using var data = SKData.Create(stream) ?? throw new InvalidDataException("The file could not be read.");
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("This file is not a supported image (PNG, JPG, WEBP, ICO, BMP, GIF).");

        var size = codec.Info;
        if (size.Width > MaxDimension || size.Height > MaxDimension)
            throw new InvalidDataException($"The image is too large ({size.Width}x{size.Height}). Maximum is {MaxDimension}px per side.");

        var buffer = new PixelBuffer(size.Width, size.Height);
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        var handle = GCHandle.Alloc(buffer.Data, GCHandleType.Pinned);
        try
        {
            var result = codec.GetPixels(info, handle.AddrOfPinnedObject());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                throw new InvalidDataException($"The image could not be decoded ({result}).");
        }
        finally
        {
            handle.Free();
        }

        return ApplyOrientation(buffer, codec.EncodedOrigin);
    }

    /// <summary>Phone photos store rotation as metadata (EXIF); apply it so the picture appears upright.</summary>
    private static PixelBuffer ApplyOrientation(PixelBuffer image, SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopRight => ImageTransforms.FlipHorizontal(image),
        SKEncodedOrigin.BottomRight => ImageTransforms.Rotate180(image),
        SKEncodedOrigin.BottomLeft => ImageTransforms.FlipVertical(image),
        SKEncodedOrigin.LeftTop => ImageTransforms.FlipHorizontal(ImageTransforms.Rotate90Clockwise(image)),
        SKEncodedOrigin.RightTop => ImageTransforms.Rotate90Clockwise(image),
        SKEncodedOrigin.RightBottom => ImageTransforms.FlipHorizontal(ImageTransforms.Rotate90CounterClockwise(image)),
        SKEncodedOrigin.LeftBottom => ImageTransforms.Rotate90CounterClockwise(image),
        _ => image,
    };
}
