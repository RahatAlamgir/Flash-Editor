using System.IO.Compression;
using System.Text.Json;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.IO.Project;

/// <summary>Saves pages as a project file: every layer with its pixels and, for shapes, text and pictures, the editable object.</summary>
public static class ProjectWriter
{
    /// <summary>Saves one picture as a project with a single page.</summary>
    public static void Save(Document document, string path) => Save(new[] { new ProjectPage("Page 1", document) }, 0, path);

    public static void Save(IReadOnlyList<ProjectPage> pages, int activePage, string path)
    {
        if (pages.Count == 0) throw new ArgumentException("A project needs at least one page.", nameof(pages));
        var store = new PixelStore();
        var manifest = BuildManifest(pages, activePage, store);
        string json = JsonSerializer.Serialize(manifest, ProjectJson.Create(store));

        // PNG encoding is the slow part of saving, and the pictures do not depend on each other.
        var pictures = store.Written.ToArray();
        var encoded = new byte[pictures.Length][];
        Parallel.For(0, pictures.Length, i => encoded[i] = ImageEncoder.EncodePng(pictures[i].Key));

        // The small picture for the Project Manager: the page that was open, flattened and shrunk.
        byte[]? preview = null;
        try { preview = MakePreview(pages[Math.Clamp(activePage, 0, pages.Count - 1)].Document); }
        catch (Exception) { /* a missing preview is only a missing thumbnail */ }

        // Write next to the target first, so a failure (full disk...) never destroys the previous file.
        string temp = path + ".saving";
        try
        {
            using (var stream = File.Create(temp))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry(ProjectFile.ManifestEntry, CompressionLevel.Optimal);
                using (var text = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false))) text.Write(json);

                if (preview != null)
                {
                    using var thumbnail = zip.CreateEntry(ProjectFile.PreviewEntry, CompressionLevel.NoCompression).Open();
                    thumbnail.Write(preview);
                }

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

    private static byte[] MakePreview(Document document)
    {
        var flat = Compositor.Flatten(document);
        double scale = Math.Min(1.0, ProjectFile.PreviewSize / (double)Math.Max(flat.Width, flat.Height));
        if (scale < 1)
            flat = Resampler.Resize(flat, Math.Max(1, (int)Math.Round(flat.Width * scale)), Math.Max(1, (int)Math.Round(flat.Height * scale)), ResampleMode.Smooth);
        return ImageEncoder.EncodePng(flat);
    }

    private static ProjectManifest BuildManifest(IReadOnlyList<ProjectPage> pages, int activePage, PixelStore store)
    {
        var manifest = new ProjectManifest { ActivePage = Math.Clamp(activePage, 0, pages.Count - 1) };
        foreach (var page in pages)
        {
            var document = page.Document;
            var entry = new PageEntry
            {
                Name = page.Name,
                Width = document.Width,
                Height = document.Height,
                ActiveLayer = Math.Max(0, document.ActiveLayerIndex),
                VerticalGuides = page.VerticalGuides.ToList(),
                HorizontalGuides = page.HorizontalGuides.ToList(),
            };
            foreach (var layer in document.Layers)
            {
                var (full, fullX, fullY) = layer.GetFullContent();
                bool extended = layer.HasHiddenPixels;
                entry.Layers.Add(new LayerEntry
                {
                    Name = layer.Name,
                    Opacity = layer.Opacity,
                    Visible = layer.IsVisible,
                    Locked = layer.IsLocked,
                    BlendMode = layer.BlendMode,
                    Adjustment = layer.Adjustment,
                    Pixels = layer.Pixels,
                    Extended = extended ? full : null,
                    ExtendedX = extended ? fullX : 0,
                    ExtendedY = extended ? fullY : 0,
                    Restore = layer.RestoreSource,
                    Object = layer.Object,
                    MaskPicture = layer.Mask?.ToGrayPicture(),
                    MaskEnabled = layer.MaskEnabled,
                });
            }
            manifest.Pages.Add(entry);
        }
        return manifest;
    }
}
