using System.IO.Compression;
using System.Text.Json;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Project;

/// <summary>Opens a project file written by <see cref="ProjectWriter"/>. Problems are reported as <see cref="InvalidDataException"/> with a plain message.</summary>
public static class ProjectReader
{
    public static Document Open(string path)
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

    private static Document Build(ProjectManifest manifest)
    {
        if (manifest.Format != ProjectFile.FormatName) throw new InvalidDataException("This is not a Flash Editor project.");
        if (manifest.Version > ProjectFile.CurrentVersion)
            throw new InvalidDataException("This project was saved by a newer version of Flash Editor. Update the program to open it.");
        if (manifest.Layers.Count == 0) throw new InvalidDataException("The project has no layers.");
        ImageDecoder.CheckSize(manifest.Width, manifest.Height);

        var document = new Document(manifest.Width, manifest.Height);
        var layers = new List<Layer>();
        foreach (var entry in manifest.Layers)
        {
            var pixels = entry.Pixels ?? throw new InvalidDataException("A layer has no picture.");
            if (pixels.Width != manifest.Width || pixels.Height != manifest.Height)
                throw new InvalidDataException("A layer does not match the size of the picture.");

            var layer = new Layer(entry.Name, pixels)
            {
                Opacity = entry.Opacity,
                IsVisible = entry.Visible,
                IsLocked = entry.Locked,
                RestoreSource = entry.Restore,
            };
            if (entry.Object != null || entry.Extended != null)
                layer.RestoreState(new LayerState(pixels, entry.Extended, entry.ExtendedX, entry.ExtendedY, entry.Object));
            layers.Add(layer);
        }
        document.SetLayers(layers, layers[Math.Clamp(manifest.ActiveLayer, 0, layers.Count - 1)]);
        return document;
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
