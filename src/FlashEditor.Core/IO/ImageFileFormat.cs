namespace FlashEditor.Core.IO;

public enum ImageFileFormat { Png, Jpeg, Webp, Ico }

/// <summary>File extensions and dialog filters for supported formats.</summary>
public static class ImageFileFormats
{
    /// <summary>Extensions that can be opened (decoding supports a few more than we save).</summary>
    public static readonly string[] OpenExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".ico", ".bmp", ".gif" };

    public const string OpenFilter =
        "All images|*.png;*.jpg;*.jpeg;*.webp;*.ico;*.bmp;*.gif|" +
        "PNG|*.png|JPEG|*.jpg;*.jpeg|WebP|*.webp|Icon|*.ico|All files|*.*";

    /// <summary>Order matches <see cref="FromFilterIndex"/>.</summary>
    public const string SaveFilter = "PNG image|*.png|JPEG image|*.jpg|WebP image|*.webp|Windows icon|*.ico";

    public static ImageFileFormat FromFilterIndex(int oneBasedIndex) => oneBasedIndex switch
    {
        2 => ImageFileFormat.Jpeg,
        3 => ImageFileFormat.Webp,
        4 => ImageFileFormat.Ico,
        _ => ImageFileFormat.Png,
    };

    public static int ToFilterIndex(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Jpeg => 2,
        ImageFileFormat.Webp => 3,
        ImageFileFormat.Ico => 4,
        _ => 1,
    };

    public static ImageFileFormat? FromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => ImageFileFormat.Png,
        ".jpg" or ".jpeg" => ImageFileFormat.Jpeg,
        ".webp" => ImageFileFormat.Webp,
        ".ico" => ImageFileFormat.Ico,
        _ => null,
    };

    public static string Extension(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Jpeg => ".jpg",
        ImageFileFormat.Webp => ".webp",
        ImageFileFormat.Ico => ".ico",
        _ => ".png",
    };

    /// <summary>JPEG and lossy WebP ask the user for a quality setting.</summary>
    public static bool HasQuality(ImageFileFormat format) => format is ImageFileFormat.Jpeg or ImageFileFormat.Webp;

    public static bool CanOpen(string path) => OpenExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
}
