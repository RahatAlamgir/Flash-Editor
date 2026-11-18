using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO;

/// <summary>What to do with every picture in a folder.</summary>
/// <param name="MaxWidth">Pictures wider than this are made smaller (0 = no limit). Pictures are never enlarged.</param>
/// <param name="MaxHeight">Same for the height (0 = no limit).</param>
public sealed record BatchOptions(string InputFolder, string OutputFolder, ImageFileFormat Format, int Quality = 90,
    int MaxWidth = 0, int MaxHeight = 0, bool IncludeSubfolders = false);

/// <summary>The outcome of a batch run.</summary>
public sealed record BatchResult(int Converted, IReadOnlyList<string> Failed);

/// <summary>
/// File > Batch Convert: turns every picture in a folder into another format and/or a smaller size in one go. The originals are never
/// changed. Existing files are never overwritten: a number is added to the new name instead.
/// </summary>
public static class BatchConverter
{
    /// <summary>The pictures that would be converted.</summary>
    public static IReadOnlyList<string> FindPictures(BatchOptions options)
    {
        if (!Directory.Exists(options.InputFolder)) return Array.Empty<string>();
        var search = options.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string output = Path.GetFullPath(options.OutputFolder).TrimEnd('\\', '/');
        return Directory.EnumerateFiles(options.InputFolder, "*", search)
            .Where(ImageFileFormats.CanOpen)
            .Where(f => !Path.GetFullPath(f).StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))   // never convert the results again
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The size a picture gets: it fits inside the limits, keeps its proportions and is never enlarged.</summary>
    public static (int Width, int Height) FitSize(int width, int height, int maxWidth, int maxHeight)
    {
        double scale = 1;
        if (maxWidth > 0) scale = Math.Min(scale, maxWidth / (double)width);
        if (maxHeight > 0) scale = Math.Min(scale, maxHeight / (double)height);
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>A file name that does not exist yet: "name.png", then "name (2).png", "name (3).png"...</summary>
    public static string UniquePath(string folder, string name, string extension)
    {
        string path = Path.Combine(folder, name + extension);
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(folder, $"{name} ({i}){extension}");
        return path;
    }

    public static BatchResult Run(BatchOptions options, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancellation = default)
    {
        var files = FindPictures(options);
        var failed = new List<string>();
        int converted = 0;
        Directory.CreateDirectory(options.OutputFolder);
        string root = Path.GetFullPath(options.InputFolder);

        for (int i = 0; i < files.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report((i, files.Count));
            string file = files[i];
            try
            {
                var image = ImageDecoder.Load(file);
                var (w, h) = FitSize(image.Width, image.Height, options.MaxWidth, options.MaxHeight);

                // Keep the folder layout when subfolders are included.
                string relative = Path.GetRelativePath(root, Path.GetDirectoryName(Path.GetFullPath(file)) ?? root);
                string folder = relative == "." ? options.OutputFolder : Path.Combine(options.OutputFolder, relative);
                Directory.CreateDirectory(folder);
                string target = UniquePath(folder, Path.GetFileNameWithoutExtension(file), ImageFileFormats.Extension(options.Format));

                ImageExporter.Save(image, target, new ExportSettings(options.Format, options.Quality, w, h));
                converted++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        progress?.Report((files.Count, files.Count));
        return new BatchResult(converted, failed);
    }
}
