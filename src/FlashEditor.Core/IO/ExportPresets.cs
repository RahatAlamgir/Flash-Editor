using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.IO;

/// <summary>How a picture is made to fit a preset size when its shape is different.</summary>
public enum ExportFit
{
    /// <summary>The whole picture is shown, as big as it can be; the rest of the preset size is filled with the background.</summary>
    Fit,

    /// <summary>The preset size is filled completely; the parts of the picture that do not fit are cut off (centered).</summary>
    Fill,

    /// <summary>The picture is stretched to the preset size (its shape changes).</summary>
    Stretch,
}

/// <summary>A ready-made size for a use of the picture (a social media post, a thumbnail, a print...).</summary>
public sealed record ExportPreset(string Group, string Name, int Width, int Height)
{
    public string Description => $"{Name}  ({Width} x {Height})";
}

/// <summary>The ready-made export sizes, and how a picture is fitted to one.</summary>
public static class ExportPresets
{
    public static IReadOnlyList<ExportPreset> All { get; } = new ExportPreset[]
    {
        new("Social media", "Instagram post (square)", 1080, 1080),
        new("Social media", "Instagram post (portrait)", 1080, 1350),
        new("Social media", "Instagram / TikTok story", 1080, 1920),
        new("Social media", "Facebook post", 1200, 630),
        new("Social media", "Facebook cover", 820, 312),
        new("Social media", "X (Twitter) post", 1600, 900),
        new("Social media", "X (Twitter) header", 1500, 500),
        new("Social media", "LinkedIn post", 1200, 627),
        new("Social media", "Pinterest pin", 1000, 1500),
        new("Video", "YouTube thumbnail", 1280, 720),
        new("Video", "YouTube channel banner", 2560, 1440),
        new("Video", "Full HD", 1920, 1080),
        new("Video", "4K Ultra HD", 3840, 2160),
        new("Print (300 dpi)", "A4 page", 2480, 3508),
        new("Print (300 dpi)", "A5 page", 1748, 2480),
        new("Print (300 dpi)", "US Letter page", 2550, 3300),
        new("Print (300 dpi)", "Photo 4 x 6 in", 1200, 1800),
        new("Print (300 dpi)", "Business card 3.5 x 2 in", 1050, 600),
        new("Web and apps", "Website banner", 1920, 600),
        new("Web and apps", "Email header", 600, 200),
        new("Web and apps", "App icon", 1024, 1024),
        new("Web and apps", "Favicon", 512, 512),
    };

    /// <summary>
    /// Makes a picture exactly <paramref name="preset"/> sized from <paramref name="source"/>. <paramref name="background"/> fills the space
    /// around the picture when <paramref name="fit"/> is <see cref="ExportFit.Fit"/> (null = see-through).
    /// </summary>
    public static PixelBuffer Compose(PixelBuffer source, ExportPreset preset, ExportFit fit, ColorRgba? background)
    {
        int w = preset.Width, h = preset.Height;
        if (source.Width <= 0 || source.Height <= 0) return new PixelBuffer(w, h);

        switch (fit)
        {
            case ExportFit.Stretch:
                return Resampler.Resize(source, w, h, ResampleMode.Lanczos);

            case ExportFit.Fill:
            {
                double scale = Math.Max(w / (double)source.Width, h / (double)source.Height);
                int cw = Math.Min(source.Width, Math.Max(1, (int)Math.Round(w / scale)));
                int ch = Math.Min(source.Height, Math.Max(1, (int)Math.Round(h / scale)));
                var crop = new PixelRect((source.Width - cw) / 2, (source.Height - ch) / 2, cw, ch);
                return Resampler.Resize(source.Crop(crop), w, h, ResampleMode.Lanczos);
            }

            default:
            {
                double scale = Math.Min(w / (double)source.Width, h / (double)source.Height);
                int fw = Math.Max(1, (int)Math.Round(source.Width * scale));
                int fh = Math.Max(1, (int)Math.Round(source.Height * scale));
                var canvas = new PixelBuffer(w, h);
                if (background is { } color) canvas.Fill(color);
                canvas.CopyFrom(Resampler.Resize(source, fw, fh, ResampleMode.Lanczos), (w - fw) / 2, (h - fh) / 2);
                return canvas;
            }
        }
    }
}
