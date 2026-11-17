using System.Windows.Media.Imaging;

namespace FlashEditor.App.Services;

/// <summary>One project in the Project Manager.</summary>
public sealed class ProjectEntry
{
    public ProjectEntry(string path, DateTime modified, long size, int pages, int width, int height, BitmapSource? thumbnail, string? error = null)
    {
        Path = path;
        Modified = modified;
        Size = size;
        Pages = pages;
        Width = width;
        Height = height;
        Thumbnail = thumbnail;
        Error = error;
    }

    public string Path { get; }
    public DateTime Modified { get; }
    public long Size { get; }
    public int Pages { get; }
    public int Width { get; }
    public int Height { get; }
    public BitmapSource? Thumbnail { get; }

    /// <summary>Set when the file could not be read (damaged, or not a Flash Editor project).</summary>
    public string? Error { get; }

    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    public bool HasThumbnail => Thumbnail != null;

    /// <summary>"3 pages - 1280 x 720".</summary>
    public string Details => Error != null ? Error : $"{Pages} page{(Pages == 1 ? "" : "s")}  •  {Width} × {Height}";

    /// <summary>"Edited today 21:30", "Edited yesterday", "Edited 12 Nov 2026".</summary>
    public string ModifiedText
    {
        get
        {
            var today = DateTime.Today;
            if (Modified.Date == today) return "Edited today " + Modified.ToString("HH:mm");
            if (Modified.Date == today.AddDays(-1)) return "Edited yesterday " + Modified.ToString("HH:mm");
            return "Edited " + Modified.ToString("d MMM yyyy");
        }
    }

    public string FolderText => System.IO.Path.GetDirectoryName(Path) ?? "";

    public string ToolTip => Path + "\n" + FormatSize(Size);

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{Math.Max(1, bytes / 1024)} KB",
        _ => $"{bytes / 1048576.0:0.#} MB",
    };
}
