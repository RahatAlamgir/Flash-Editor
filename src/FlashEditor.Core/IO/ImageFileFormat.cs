namespace FlashEditor.Core.IO;

/// <summary>Formats the program can write. (It can open more: see <see cref="ImageFileFormats.OpenExtensions"/>.)</summary>
public enum ImageFileFormat { Png, Jpeg, Webp, Ico, Bmp, Tiff, Gif }

/// <summary>File extensions, dialog filters and short descriptions for the supported formats.</summary>
public static class ImageFileFormats
{
    // Read by the built-in decoder (SkiaSharp).
    private static readonly string[] CommonExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".ico", ".bmp", ".gif" };

    // Read through Windows' own image codecs (TIFF and JPEG XR always; the rest when Windows has the codec installed).
    private static readonly string[] WindowsExtensions =
    {
        ".tif", ".tiff", ".jxr", ".wdp", ".hdp", ".dds",
        ".heic", ".heif", ".avif", ".jxl",
        ".dng", ".cr2", ".cr3", ".nef", ".arw", ".orf", ".rw2", ".raf",
    };

    /// <summary>Extensions that can be opened (decoding supports more than we save).</summary>
    public static readonly string[] OpenExtensions = CommonExtensions.Concat(WindowsExtensions).ToArray();

    private static string Pattern(IEnumerable<string> extensions) => string.Join(";", extensions.Select(e => "*" + e));

    public static readonly string OpenFilter =
        "All images|" + Pattern(OpenExtensions) + "|" +
        "PNG|*.png|JPEG|*.jpg;*.jpeg|WebP|*.webp|Icon|*.ico|Bitmap|*.bmp|GIF|*.gif|" +
        "TIFF|*.tif;*.tiff|HEIC / AVIF / JPEG XL|*.heic;*.heif;*.avif;*.jxl|" +
        "Camera RAW|*.dng;*.cr2;*.cr3;*.nef;*.arw;*.orf;*.rw2;*.raf|JPEG XR|*.jxr;*.wdp;*.hdp|DDS texture|*.dds|All files|*.*";

    /// <summary>File > Open: pictures and Flash Editor projects.</summary>
    public static readonly string OpenAnyFilter =
        "Pictures and projects|*" + Project.ProjectFile.Extension + ";" + Pattern(OpenExtensions) + "|" +
        Project.ProjectFile.Filter + "|" + OpenFilter;

    /// <summary>Order matches <see cref="FromFilterIndex"/>.</summary>
    public const string SaveFilter =
        "PNG image|*.png|JPEG image|*.jpg|WebP image|*.webp|Windows icon|*.ico|Bitmap (BMP)|*.bmp|TIFF image|*.tif|GIF image (256 colors)|*.gif";

    public static ImageFileFormat FromFilterIndex(int oneBasedIndex) => oneBasedIndex switch
    {
        2 => ImageFileFormat.Jpeg,
        3 => ImageFileFormat.Webp,
        4 => ImageFileFormat.Ico,
        5 => ImageFileFormat.Bmp,
        6 => ImageFileFormat.Tiff,
        7 => ImageFileFormat.Gif,
        _ => ImageFileFormat.Png,
    };

    public static int ToFilterIndex(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Jpeg => 2,
        ImageFileFormat.Webp => 3,
        ImageFileFormat.Ico => 4,
        ImageFileFormat.Bmp => 5,
        ImageFileFormat.Tiff => 6,
        ImageFileFormat.Gif => 7,
        _ => 1,
    };

    public static ImageFileFormat? FromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => ImageFileFormat.Png,
        ".jpg" or ".jpeg" => ImageFileFormat.Jpeg,
        ".webp" => ImageFileFormat.Webp,
        ".ico" => ImageFileFormat.Ico,
        ".bmp" => ImageFileFormat.Bmp,
        ".tif" or ".tiff" => ImageFileFormat.Tiff,
        ".gif" => ImageFileFormat.Gif,
        _ => null,
    };

    public static string Extension(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Jpeg => ".jpg",
        ImageFileFormat.Webp => ".webp",
        ImageFileFormat.Ico => ".ico",
        ImageFileFormat.Bmp => ".bmp",
        ImageFileFormat.Tiff => ".tif",
        ImageFileFormat.Gif => ".gif",
        _ => ".png",
    };

    /// <summary>JPEG and lossy WebP ask the user for a quality setting.</summary>
    public static bool HasQuality(ImageFileFormat format) => format is ImageFileFormat.Jpeg or ImageFileFormat.Webp;

    public static bool CanOpen(string path) => OpenExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// What to tell the user when a file with this extension could not be read: for formats that need a free
    /// Windows extension, which one to install; otherwise a general message.
    /// </summary>
    public static string CannotOpenHint(string? extension)
    {
        string name = string.IsNullOrEmpty(extension) ? "This file" : $"This {extension.TrimStart('.').ToUpperInvariant()} file";
        return (extension ?? "").ToLowerInvariant() switch
        {
            ".heic" or ".heif" =>
                $"{name} could not be opened. Windows needs the free \"HEIF Image Extensions\" (Microsoft Store) to read HEIC photos. Install it, then try again.",
            ".avif" =>
                $"{name} could not be opened. Windows needs the free \"AV1 Video Extension\" (Microsoft Store) to read AVIF images. Install it, then try again.",
            ".jxl" =>
                $"{name} could not be opened. Windows needs the \"JPEG XL Image Extension\" (Microsoft Store) to read JPEG XL images.",
            ".dng" or ".cr2" or ".cr3" or ".nef" or ".arw" or ".orf" or ".rw2" or ".raf" =>
                $"{name} could not be opened. Windows needs the free \"Raw Image Extension\" (Microsoft Store) to read camera RAW files.",
            _ => $"{name} is not a supported image, or it is damaged. Supported: PNG, JPG, WEBP, ICO, BMP, GIF, TIFF, JPEG XR, DDS (and HEIC, AVIF, RAW when Windows has the codec).",
        };
    }
}
