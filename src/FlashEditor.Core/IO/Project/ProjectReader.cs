using System.IO.Compression;
using System.Text.Json;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Project;

/// <summary>Opens a project file written by <see cref="ProjectWriter"/>. Problems are reported as <see cref="InvalidDataException"/> with a plain message.</summary>
public static class ProjectReader
{
    /// <summary>Opens a project and returns its first page (the rest is ignored). See <see cref="OpenProject"/> for all pages.</summary>
    public static Document Open(string path) => OpenProject(path).Pages[0].Document;

    /// <summary>
    /// Reads only what the Project Manager needs: the number of pages, the size, the page names and the small preview picture.
    /// Does not decode the pictures of the layers, so it is fast even for big projects.
    /// </summary>
    public static ProjectInfo ReadInfo(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry(ProjectFile.ManifestEntry) ?? throw new InvalidDataException("This is not a Flash Editor project.");
            using var stream = entry.Open();
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;

            var names = new List<string>();
            int width = 0, height = 0, count = 0;
            if (Find(root, "Pages") is { ValueKind: JsonValueKind.Array } pages && pages.GetArrayLength() > 0)
            {
                foreach (var page in pages.EnumerateArray())
                {
                    if (count++ == 0)
                    {
                        width = Find(page, "Width")?.GetInt32() ?? 0;
                        height = Find(page, "Height")?.GetInt32() ?? 0;
                    }
                    names.Add(Find(page, "Name")?.GetString() ?? "Page " + count);
                }
            }
            else
            {
                count = 1;                                   // a version 1 file: one page written straight into the manifest
                width = Find(root, "Width")?.GetInt32() ?? 0;
                height = Find(root, "Height")?.GetInt32() ?? 0;
                names.Add("Page 1");
            }

            PixelBuffer? preview = null;
            if (zip.GetEntry(ProjectFile.PreviewEntry) is { } thumbnail)
            {
                try
                {
                    using var input = thumbnail.Open();
                    using var memory = new MemoryStream();
                    input.CopyTo(memory);
                    memory.Position = 0;
                    preview = ImageDecoder.Load(memory, ".png");
                }
                catch (Exception) { /* a damaged preview is just no preview */ }
            }
            return new ProjectInfo(count, width, height, names, preview);
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException or NotSupportedException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("The project file could not be read (" + e.Message + ").", e);
        }
    }

    // Property names are written in PascalCase, but a hand-edited file may use another case.
    private static JsonElement? Find(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        return null;
    }

    public static ProjectContent OpenProject(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var cache = new Dictionary<string, PixelBuffer>();
            var store = new PixelStore(name => LoadImage(zip, name, cache));

            var entry = zip.GetEntry(ProjectFile.ManifestEntry) ?? throw new InvalidDataException("This is not a Flash Editor project.");
            ProjectManifest manifest;
            using (var stream = entry.Open())
                manifest = JsonSerializer.Deserialize<ProjectManifest>(stream, ProjectJson.Create(store))
                           ?? throw new InvalidDataException("The project file is empty.");

            return Build(manifest);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException or ArgumentException or KeyNotFoundException)
        {
            throw new InvalidDataException("The project file is damaged and could not be read (" + e.Message + ").", e);
        }
    }

    private static ProjectContent Build(ProjectManifest manifest)
    {
        if (manifest.Format != ProjectFile.FormatName) throw new InvalidDataException("This is not a Flash Editor project.");
        if (manifest.Version > ProjectFile.CurrentVersion)
            throw new InvalidDataException("This project was saved by a newer version of Flash Editor. Update the program to open it.");

        // Version 1 files hold one page straight in the manifest.
        var entries = manifest.Pages.Count > 0
            ? manifest.Pages
            : new List<PageEntry> { new() { Name = "Page 1", Width = manifest.Width, Height = manifest.Height, ActiveLayer = manifest.ActiveLayer, Layers = manifest.Layers } };

        var pages = new List<ProjectPage>();
        foreach (var entry in entries) pages.Add(BuildPage(entry));
        return new ProjectContent(pages, Math.Clamp(manifest.ActivePage, 0, pages.Count - 1));
    }

    private static ProjectPage BuildPage(PageEntry page)
    {
        if (page.Layers.Count == 0) throw new InvalidDataException("A page has no layers.");
        ImageDecoder.CheckSize(page.Width, page.Height);

        var document = new Document(page.Width, page.Height);
        var layers = new List<Layer>();
        foreach (var entry in page.Layers)
        {
            var pixels = entry.Pixels ?? throw new InvalidDataException("A layer has no picture.");
            if (pixels.Width != page.Width || pixels.Height != page.Height)
                throw new InvalidDataException("A layer does not match the size of the picture.");

            var layer = new Layer(entry.Name, pixels)
            {
                Opacity = entry.Opacity,
                IsVisible = entry.Visible,
                IsLocked = entry.Locked,
                BlendMode = entry.BlendMode,
                Adjustment = entry.Adjustment,
                RestoreSource = entry.Restore,
            };
            if (entry.Object != null || entry.Extended != null)
                layer.RestoreState(new LayerState(pixels, entry.Extended, entry.ExtendedX, entry.ExtendedY, entry.Object));
            layers.Add(layer);
        }
        document.SetLayers(layers, layers[Math.Clamp(page.ActiveLayer, 0, layers.Count - 1)]);
        return new ProjectPage(page.Name, document, page.VerticalGuides, page.HorizontalGuides);
    }

    private static PixelBuffer LoadImage(ZipArchive zip, string name, Dictionary<string, PixelBuffer> cache)
    {
        if (cache.TryGetValue(name, out var known)) return known;
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException("A picture is missing from the project file (" + name + ").");
        using var input = entry.Open();
        using var memory = new MemoryStream();
        input.CopyTo(memory);
        memory.Position = 0;
        var image = ImageDecoder.Load(memory, ".png");
        cache[name] = image;
        return image;
    }
}
