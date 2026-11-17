using System.IO;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects;
using FlashEditor.Core.History;
using FlashEditor.Core.IO;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>File menu: New, Open, Place, Save / Save As (the editable project) and Export (a normal picture).</summary>
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
        var path = _main.Dialogs.ShowOpenFile("Open");
        if (path != null) OpenPath(path);
    }

    /// <summary>Opens a picture or a saved project in a new tab (or shows its tab if it is already open). Used by the menu, drag &amp; drop and the command line.</summary>
    public void OpenPath(string path)
    {
        if (_main.ActivateIfOpen(path)) return;
        if (ProjectFile.IsProject(path))
        {
            OpenProject(path);
            return;
        }
        _main.RunAsync("Opening the image", async () =>
        {
            var pixels = await Task.Run(() => ImageDecoder.Load(path));
            _main.LoadDocument(Document.FromImage(pixels), path, "Open");
            _main.SetStatus($"Opened {Path.GetFileName(path)} ({pixels.Width} x {pixels.Height})");
        });
    }

    private void OpenProject(string path)
    {
        _main.RunAsync("Opening the project", async () =>
        {
            var document = await Task.Run(() => ProjectReader.Open(path));
            _main.LoadDocument(document, path, "Open Project");
            _main.SetStatus($"Opened {Path.GetFileName(path)} ({document.Width} x {document.Height}, {document.Layers.Count} layers)");
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

    /// <summary>
    /// Saves the project (a .flash file with every layer, shape and text still editable). To get a normal picture
    /// (PNG, JPG...) use Export. Returns false if cancelled or failed.
    /// </summary>
    public bool Save(bool saveAs)
    {
        string? path = Doc.FilePath;
        bool isProject = path != null && ProjectFile.IsProject(path);

        if (saveAs || !isProject)
        {
            var suggested = Path.GetFileNameWithoutExtension(Doc.DisplayName);
            string? folder = path != null ? Path.GetDirectoryName(path) : null;
            path = _main.Dialogs.ShowSaveProject(suggested, folder);
            if (path == null) return false;
            if (!ProjectFile.IsProject(path)) path += ProjectFile.Extension;
        }

        bool ok = false;
        _main.Run("Saving", () =>
        {
            ProjectWriter.Save(Doc.Model, path!);
            Doc.FilePath = path;
            Doc.Model.History.MarkSaved();
            ok = true;
        });
        if (ok) _main.SetStatus($"Saved project {Path.GetFileName(path)}. Use File > Export to make a PNG, JPG or other picture.");
        return ok;
    }

    /// <summary>
    /// Makes a normal picture from the project with a chosen format, quality and size (e.g. a smaller JPG for the web,
    /// or an enlarged PNG). The open document is not changed and keeps its own file name.
    /// </summary>
    private void Export()
    {
        var flat = Compositor.Flatten(Doc.Model);
        var format = Doc.FileFormat ?? ImageFileFormat.Png;
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
