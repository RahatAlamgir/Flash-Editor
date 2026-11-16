using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.IO;

/// <summary>
/// How to write an image file: format, quality (JPG/WebP), and the output size in pixels
/// (the picture is resized only in the file; the open document is not changed).
/// <see cref="Crop"/> (optional) cuts out part of the picture first; Width/Height are the size of that part.
/// </summary>
public sealed record ExportSettings(
    ImageFileFormat Format,
    int Quality,
    int Width,
    int Height,
    ResampleMode Resample = ResampleMode.Lanczos,
    ColorRgba? Matte = null,
    PixelRect? Crop = null);
