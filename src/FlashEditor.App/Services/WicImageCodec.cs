using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;

namespace FlashEditor.App.Services;

/// <summary>
/// Connects the engine's file reading / writing to Windows' own image codecs (WIC), which know many more formats than
/// the built-in decoder: TIFF, JPEG XR, DDS, and, when Windows has the codec (free in the Microsoft Store), HEIC, AVIF,
/// JPEG XL and camera RAW files. Also writes TIFF.
/// </summary>
public static class WicImageCodec
{
    /// <summary>Call once at startup.</summary>
    public static void Register()
    {
        ImageDecoder.FallbackDecoder = Decode;
        ImageEncoder.FallbackEncoder = Encode;
    }

    /// <summary>Reads the first picture of a file Windows understands, upright, as straight-alpha BGRA.</summary>
    public static PixelBuffer? Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        // OnLoad: read everything now so the stream can be closed. Ignore the color profile: we want plain sRGB-like values.
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) return null;

        var frame = decoder.Frames[0];
        ImageDecoder.CheckSize(frame.PixelWidth, frame.PixelHeight);

        BitmapSource source = frame.Format == PixelFormats.Bgra32 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var image = BitmapConverter.FromBitmapSource(source);
        return ImageDecoder.ApplyExifOrientation(image, ReadOrientation(frame));
    }

    /// <summary>The EXIF orientation (1..8) stored in the file, or 1 when there is none.</summary>
    private static int ReadOrientation(BitmapFrame frame)
    {
        if (frame.Metadata is not BitmapMetadata metadata) return 1;
        foreach (var query in new[] { "System.Photo.Orientation", "/ifd/{ushort=274}", "/app1/ifd/{ushort=274}" })
        {
            try
            {
                if (metadata.ContainsQuery(query) && Convert.ToInt32(metadata.GetQuery(query)) is >= 1 and <= 8 and var value)
                    return value;
            }
            catch (Exception)
            {
                // this file does not have that kind of metadata: try the next place
            }
        }
        return 1;
    }

    /// <summary>Writes a TIFF (lossless, compressed). GIF is written by the engine itself (see <see cref="GifEncoder"/>).</summary>
    public static byte[] Encode(PixelBuffer image, ImageFileFormat format)
    {
        if (format != ImageFileFormat.Tiff) throw new NotSupportedException($"{format} is not written through Windows' codecs.");

        var encoder = new TiffBitmapEncoder { Compression = TiffCompressOption.Zip };
        encoder.Frames.Add(BitmapFrame.Create(BitmapConverter.ToFrozenBitmap(image)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
