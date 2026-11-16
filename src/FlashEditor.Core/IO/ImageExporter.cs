using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.IO;

/// <summary>Turns a flattened image into file bytes using <see cref="ExportSettings"/> (resize + encode).</summary>
public static class ImageExporter
{
    /// <summary>The image cropped (if asked) and at the export size. The input is not changed.</summary>
    public static PixelBuffer Resize(PixelBuffer image, ExportSettings settings)
    {
        if (settings.Crop is { } crop && crop != image.Bounds && !crop.Intersect(image.Bounds).IsEmpty)
            image = image.Crop(crop.Intersect(image.Bounds));
        int w = Math.Clamp(settings.Width, 1, ImageDecoder.MaxDimension);
        int h = Math.Clamp(settings.Height, 1, ImageDecoder.MaxDimension);
        return w == image.Width && h == image.Height
            ? image
            : Resampler.Resize(image, w, h, settings.Resample);
    }

    public static byte[] Encode(PixelBuffer image, ExportSettings settings)
        => ImageEncoder.Encode(Resize(image, settings), settings.Format, settings.Quality, settings.Matte);

    public static void Save(PixelBuffer image, string path, ExportSettings settings)
        => ImageEncoder.Save(Resize(image, settings), path, settings.Format, settings.Quality, settings.Matte);

    /// <summary>"12.3 KB", "4.56 MB"...</summary>
    public static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.##} MB",
    };
}
