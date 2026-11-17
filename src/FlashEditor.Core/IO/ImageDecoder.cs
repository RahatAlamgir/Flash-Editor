using System.Runtime.InteropServices;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;
using SkiaSharp;

namespace FlashEditor.Core.IO;

/// <summary>
/// Reads image files into a <see cref="PixelBuffer"/>. PNG, JPEG, WebP, ICO, BMP and GIF are read directly (SkiaSharp).
/// Other formats (TIFF, JPEG XR, DDS, HEIC, AVIF, camera RAW...) go to <see cref="FallbackDecoder"/>, which the
/// application connects to Windows' own image codecs.
/// </summary>
public static class ImageDecoder
{
    /// <summary>Largest side we accept, to avoid running out of memory on giant images.</summary>
    public const int MaxDimension = 16384;

    /// <summary>
    /// Reads image bytes the built-in decoder does not know. Returns the picture (already upright), or null / throws
    /// when it cannot. Set by the app at startup; null means only the built-in formats can be opened.
    /// </summary>
    public static Func<byte[], PixelBuffer?>? FallbackDecoder { get; set; }

    public static PixelBuffer Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream, Path.GetExtension(path));
    }

    /// <param name="extension">The file's extension (like ".heic"), only used to give a helpful message when reading fails.</param>
    public static PixelBuffer Load(Stream stream, string? extension = null)
    {
        byte[] bytes = ReadAll(stream);
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec == null) return LoadWithFallback(bytes, extension);

        var size = codec.Info;
        CheckSize(size.Width, size.Height);

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

    private static PixelBuffer LoadWithFallback(byte[] bytes, string? extension)
    {
        PixelBuffer? result = null;
        if (FallbackDecoder is { } decode)
        {
            try { result = decode(bytes); }
            catch (InvalidDataException) { throw; } // already a clear message (for example "too large")
            catch (Exception) { result = null; }     // the codec is missing or the file is damaged
        }
        return result ?? throw new InvalidDataException(ImageFileFormats.CannotOpenHint(extension));
    }

    public static void CheckSize(int width, int height)
    {
        if (width > MaxDimension || height > MaxDimension)
            throw new InvalidDataException($"The image is too large ({width}x{height}). Maximum is {MaxDimension}px per side.");
    }

    private static byte[] ReadAll(Stream stream)
    {
        if (stream is MemoryStream { Position: 0 } memory && memory.TryGetBuffer(out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length)
            return segment.Array;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>Phone photos store rotation as metadata (EXIF); apply it so the picture appears upright.</summary>
    private static PixelBuffer ApplyOrientation(PixelBuffer image, SKEncodedOrigin origin)
        => ApplyExifOrientation(image, (int)origin);

    /// <summary>
    /// Turns a picture upright for an EXIF orientation value (1 = already upright, 2..8 = mirrored / rotated).
    /// For decoders that read the value themselves.
    /// </summary>
    public static PixelBuffer ApplyExifOrientation(PixelBuffer image, int exifOrientation) => (SKEncodedOrigin)exifOrientation switch
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
