using System.IO.Compression;
using System.Text.Json;
using FlashEditor.Core.Documents;

namespace FlashEditor.Core.IO.Project;

/// <summary>Saves a document as a project file: every layer with its pixels and, for shapes, text and pictures, the editable object.</summary>
public static class ProjectWriter
{
    public static void Save(Document document, string path)
    {
        var store = new PixelStore();
        var manifest = BuildManifest(document, store);
        string json = JsonSerializer.Serialize(manifest, ProjectJson.Create(store));

        // PNG encoding is the slow part of saving, and the pictures do not depend on each other.
        var pictures = store.Written.ToArray();
        var encoded = new byte[pictures.Length][];
        Parallel.For(0, pictures.Length, i => encoded[i] = ImageEncoder.EncodePng(pictures[i].Key));

        // Write next to the target first, so a failure (full disk...) never destroys the previous file.
        string temp = path + ".saving";
        try
        {
            using (var stream = File.Create(temp))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry(ProjectFile.ManifestEntry, CompressionLevel.Optimal);
                using (var text = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false))) text.Write(json);

                for (int i = 0; i < pictures.Length; i++)
                {
                    var image = zip.CreateEntry(pictures[i].Value, CompressionLevel.NoCompression); // PNG is already compressed
                    using var output = image.Open();
                    output.Write(encoded[i]);
                }
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static ProjectManifest BuildManifest(Document document, PixelStore store)
    {
        var manifest = new ProjectManifest
        {
            Width = document.Width,
            Height = document.Height,
            ActiveLayer = Math.Max(0, document.ActiveLayerIndex),
        };
        foreach (var layer in document.Layers)
        {
            var (full, fullX, fullY) = layer.GetFullContent();
            bool extended = layer.HasHiddenPixels;
            manifest.Layers.Add(new LayerEntry
            {
                Name = layer.Name,
                Opacity = layer.Opacity,
                Visible = layer.IsVisible,
                Locked = layer.IsLocked,
                Pixels = layer.Pixels,
                Extended = extended ? full : null,
                ExtendedX = extended ? fullX : 0,
                ExtendedY = extended ? fullY : 0,
                Restore = layer.RestoreSource,
                Object = layer.Object,
            });
        }
        return manifest;
    }
}
