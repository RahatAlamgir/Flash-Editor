using System.IO;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects;
using FlashEditor.Core.History;
using FlashEditor.Core.IO;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>File menu: New, Open, Place, Save, Save As, Export.</summary>
public sealed class FileCommands
{
    private readonly MainViewModel _main;

    public FileCommands(MainViewModel main)
    {
        _main = main;
        NewCommand = new RelayCommand(New);
        OpenCommand = new RelayCommand(Open);
        PlaceCommand = new RelayCommand(Place);
        SaveCommand = new RelayCommand(() => Save(saveAs: false));
        SaveAsCommand = new RelayCommand(() => Save(saveAs: true));
        ExportCommand = new RelayCommand(Export);
    }

    public RelayCommand NewCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand PlaceCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExportCommand { get; }

    private DocumentViewModel Doc => _main.Document;

    private void New()
    {
        var settings = _main.Dialogs.ShowNewImage(_main.Colors.Background);
        if (settings == null) return;
        _main.LoadDocument(Document.CreateBlank(settings.Width, settings.Height, settings.Background), null, "New Image");
        _main.SetStatus($"New image {settings.Width} x {settings.Height}");
    }

    private void Open()
    {
        var path = _main.Dialogs.ShowOpenImage("Open Image");
        if (path != null) OpenPath(path);
    }

    /// <summary>Opens an image file (menu, drag &amp; drop, or command line).</summary>
    /// <summary>Opens an image file in a new tab (or shows its tab if it is already open).</summary>
    public void OpenPath(string path)
    {
        if (_main.ActivateIfOpen(path)) return;
        _main.RunAsync("Opening the image", async () =>
        {
            var pixels = await Task.Run(() => ImageDecoder.Load(path));
            _main.LoadDocument(Document.FromImage(pixels), path, "Open");
            _main.SetStatus($"Opened {Path.GetFileName(path)} ({pixels.Width} x {pixels.Height})");
        });
    }

    /// <summary>Adds another image file as a new layer in the current document.</summary>
    private void Place()
    {
        var path = _main.Dialogs.ShowOpenImage("Place Image as Layer");
        if (path != null) PlacePath(path);
    }

    public void PlacePath(string path)
    {
        _main.RunAsync("Placing the image", async () =>
        {
            var pixels = await Task.Run(() => ImageDecoder.Load(path));
            var doc = Doc.Model;
            // A resizable image object, centered, scaled down to fit when it is larger than the canvas.
            var image = ImageObject.CenteredOn(pixels, doc.Width, doc.Height, Path.GetFileNameWithoutExtension(path), maxFill: 0.9);
            _main.AddObjectLayer(image, "Place Image");
            _main.SelectTool<Tools.Selecting.MoveTool>();
            _main.SetStatus($"Placed {Path.GetFileName(path)}. Drag the handles to resize or rotate it.");
        });
    }

    /// <summary>Saves (merging all visible layers). Returns false if cancelled or failed.</summary>
    public bool Save(bool saveAs)
    {
        string? path = Doc.FilePath;
        var format = Doc.FileFormat;
        int quality = Doc.SaveQuality;

        if (saveAs || path == null || format == null)
        {
            var suggested = Path.GetFileNameWithoutExtension(Doc.DisplayName);
            var target = _main.Dialogs.ShowSaveImage(suggested, format);
            if (target == null) return false;
            path = target.Path;
            format = target.Format;

            if (ImageFileFormats.HasQuality(format.Value))
            {
                var parameter = new EffectParameter("quality", "Quality", 1, 100, quality, 1, "%");
                var values = new EffectValues(new[] { parameter });
                var note = format == ImageFileFormat.Webp
                    ? "Higher = better looking but bigger file. 100 saves WebP without any loss."
                    : "Higher = better looking but bigger file. 85-92 is a good balance.";
                var chosen = _main.Dialogs.ShowParameters($"{format} Quality", new[] { parameter }, values, null, note);
                if (chosen == null) return false;
                quality = (int)chosen["quality"];
            }
        }

        bool ok = false;
        _main.Run("Saving", () =>
        {
            var flat = Compositor.Flatten(Doc.Model);
            ImageEncoder.Save(flat, path, format!.Value, quality, _main.Colors.Background);
            Doc.FilePath = path;
            Doc.FileFormat = format;
            Doc.SaveQuality = quality;
            Doc.Model.History.MarkSaved();
            ok = true;
        });
        if (ok)
        {
            string extra = format == ImageFileFormat.Ico ? " (icon sizes 16-256 px)" : "";
            _main.SetStatus($"Saved {Path.GetFileName(path)}{extra}");
        }
        return ok;
    }

    /// <summary>
    /// Saves a copy with a chosen format, quality and size (e.g. a smaller JPG for the web, or an enlarged PNG).
    /// The open document is not changed and keeps its own file name.
    /// </summary>
    private void Export()
    {
        var flat = Compositor.Flatten(Doc.Model);
        var format = Doc.FileFormat ?? ImageFileFormat.Jpeg;
        var settings = _main.Dialogs.ShowExport(flat, format, Doc.SaveQuality, _main.Colors.Background);
        if (settings == null) return;

        var suggested = Path.GetFileNameWithoutExtension(Doc.DisplayName) + $"-{settings.Width}x{settings.Height}";
        var target = _main.Dialogs.ShowSaveImage(suggested, settings.Format);
        if (target == null) return;
        settings = settings with { Format = target.Format }; // the user may pick another type in the file dialog

        _main.RunAsync("Exporting", async () =>
        {
            await Task.Run(() => ImageExporter.Save(flat, target.Path, settings));
            long bytes = new FileInfo(target.Path).Length;
            _main.SetStatus($"Exported {Path.GetFileName(target.Path)}: {settings.Width} x {settings.Height} px, {ImageExporter.FormatFileSize(bytes)}");
        });
    }

    /// <summary>Asks to save unsaved changes. Returns false if the user cancelled.</summary>
    public bool ConfirmDiscardChanges()
    {
        if (!Doc.IsDirty) return true;
        return _main.Dialogs.AskSaveChanges(Doc.DisplayName) switch
        {
            Services.SaveChangesAnswer.Save => Save(saveAs: false),
            Services.SaveChangesAnswer.Discard => true,
            _ => false,
        };
    }
}
