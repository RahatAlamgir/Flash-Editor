using System.IO;
using FlashEditor.App.Services;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
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
        AddPageCommand = new RelayCommand(AddPage);
        ProjectManagerCommand = new RelayCommand(() => ShowProjectManager(atStartup: false));
        DuplicatePageCommand = new RelayCommand(DuplicatePage);
        ExportAllPagesCommand = new RelayCommand(ExportAllPages, () => Doc.Project.Pages.Count > 1);
    }

    public RelayCommand NewCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand PlaceCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand AddPageCommand { get; }
    public RelayCommand ProjectManagerCommand { get; }
    public RelayCommand DuplicatePageCommand { get; }
    public RelayCommand ExportAllPagesCommand { get; }

    private DocumentViewModel Doc => _main.Document;

    /// <summary>
    /// Shows the Project Manager and does what the user chose: open a project, create one, or just go on. Returns false when the manager was
    /// closed at start-up (the program should quit).
    /// </summary>
    public bool ShowProjectManager(bool atStartup)
    {
        var result = _main.Dialogs.ShowProjectManager(atStartup);
        if (result == null) return !atStartup;
        switch (result.Action)
        {
            case ProjectManagerAction.Open when result.Path != null: OpenPath(result.Path); break;
            case ProjectManagerAction.New when result.New != null: CreateProject(result.New); break;
        }
        return true;
    }

    /// <summary>A new project with the chosen name, size and layout. It is saved at once in the projects folder, so it shows up in the manager.</summary>
    public void CreateProject(NewImageSettings settings)
    {
        string path = ProjectLibrary.Default.NewProjectPath(settings.Name ?? "");
        var model = Document.CreateBlank(settings.Width, settings.Height, settings.Background);
        Services.DocumentTemplates.Apply(model, settings.Template);
        _main.LoadDocument(model, path, "New Project");
        if (Save(saveAs: false)) _main.SetStatus($"Created project {Path.GetFileNameWithoutExtension(path)} in {Path.GetDirectoryName(path)}");
    }

    private void New()
    {
        var settings = _main.Dialogs.ShowNewImage(_main.Colors.Background);
        if (settings == null) return;
        var blank = Document.CreateBlank(settings.Width, settings.Height, settings.Background);
        Services.DocumentTemplates.Apply(blank, settings.Template);
        _main.LoadDocument(blank, null, "New Image");
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
            var content = await Task.Run(() => ProjectReader.OpenProject(path));
            ProjectGroup? group = null;
            var opened = new List<DocumentViewModel>();
            foreach (var page in content.Pages)
            {
                var vm = _main.LoadDocument(page.Document, path, "Open Project", into: group);
                group ??= vm.Project;
                vm.VerticalGuides.AddRange(page.VerticalGuides);
                vm.HorizontalGuides.AddRange(page.HorizontalGuides);
                vm.CustomName = page.Name;
                opened.Add(vm);
            }
            _main.ShowDocument(opened[content.ActivePage]);
            ProjectLibrary.Default.AddRecent(path);
            var first = content.Pages[content.ActivePage].Document;
            _main.SetStatus(content.Pages.Count > 1
                ? $"Opened {Path.GetFileName(path)} ({content.Pages.Count} pages)"
                : $"Opened {Path.GetFileName(path)} ({first.Width} x {first.Height}, {first.Layers.Count} layers)");
        });
    }

    /// <summary>A new empty page after the current one, the same size. All pages are saved together in one project file.</summary>
    private void AddPage()
    {
        var model = Core.Documents.Document.CreateBlank(Doc.Width, Doc.Height, ColorRgba.White);
        var page = _main.AddPage(model, NextPageName());
        _main.SetStatus($"Added {page.PageTitle}. Pages are saved together with Save (Ctrl+S).");
    }

    /// <summary>A copy of the current page (layers, shapes and text stay editable) right after it.</summary>
    private void DuplicatePage()
    {
        var copy = DocumentCopy.Copy(Doc.Model);
        var source = Doc;
        var page = _main.AddPage(copy, source.PageTitle + " copy");
        page.VerticalGuides.AddRange(source.VerticalGuides);
        page.HorizontalGuides.AddRange(source.HorizontalGuides);
        _main.SetStatus($"Duplicated the page as {page.PageTitle}.");
    }

    private string NextPageName()
    {
        var group = Doc.Project;
        var names = group.Pages.Select(p => p.PageTitle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int i = group.Pages.Count + 1; ; i++)
            if (!names.Contains($"Page {i}")) return $"Page {i}";
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
    /// Saves the project (a .flash file with every page and layer; shapes and text stay editable). To get a normal picture
    /// (PNG, JPG...) use Export. Returns false if cancelled or failed.
    /// </summary>
    public bool Save(bool saveAs)
    {
        string? path = Doc.FilePath;
        bool isProject = path != null && ProjectFile.IsProject(path);

        if (saveAs || !isProject)
        {
            var suggested = Path.GetFileNameWithoutExtension(path != null && ProjectFile.IsProject(path) ? path : Doc.DisplayName);
            string? folder = path != null ? Path.GetDirectoryName(path) : null;
            path = _main.Dialogs.ShowSaveProject(suggested, folder);
            if (path == null) return false;
            if (!ProjectFile.IsProject(path)) path += ProjectFile.Extension;
        }

        var pages = Doc.Project.Pages.ToList();
        bool ok = false;
        _main.Run("Saving", () =>
        {
            var content = pages.Select((p, i) => new ProjectPage(p.PageTitle, p.Model, p.VerticalGuides.ToList(), p.HorizontalGuides.ToList())).ToList();
            ProjectWriter.Save(content, Math.Max(0, pages.IndexOf(Doc)), path!);
            foreach (var page in pages)
            {
                page.FilePath = path;
                page.Model.History.MarkSaved();
                page.IsRecovered = false;
                page.RefreshDirty();
            }
            Doc.Project.CustomName = null;          // the project is now named after its file
            Doc.Project.StructureDirty = false;
            ok = true;
        });
        if (ok)
        {
            ProjectLibrary.Default.AddRecent(path!);
            string what = pages.Count > 1 ? $"project {Path.GetFileName(path)} ({pages.Count} pages)" : $"project {Path.GetFileName(path)}";
            _main.SetStatus($"Saved {what}. Use File > Export to make a PNG, JPG or other picture.");
        }
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

    /// <summary>Exports every page as its own picture with the chosen format, quality and size (file names get -1, -2, ...).</summary>
    private void ExportAllPages()
    {
        var pages = Doc.Project.Pages.ToList();
        if (pages.Count < 2) return;

        var current = Compositor.Flatten(Doc.Model);
        var settings = _main.Dialogs.ShowExport(current, Doc.FileFormat ?? ImageFileFormat.Png, Doc.SaveQuality, _main.Colors.Background);
        if (settings == null) return;
        var target = _main.Dialogs.ShowSaveImage(Path.GetFileNameWithoutExtension(Doc.FilePath ?? Doc.DisplayName), settings.Format);
        if (target == null) return;
        settings = settings with { Format = target.Format };

        // Every page is exported at the same enlargement as the one that was shown in the dialog.
        double scaleX = settings.Width / (double)(settings.Crop?.Width ?? current.Width);
        double scaleY = settings.Height / (double)(settings.Crop?.Height ?? current.Height);
        var images = pages.Select(p => Compositor.Flatten(p.Model)).ToList();

        _main.RunAsync("Exporting all pages", async () =>
        {
            string folder = Path.GetDirectoryName(target.Path) ?? "";
            string name = Path.GetFileNameWithoutExtension(target.Path), extension = Path.GetExtension(target.Path);
            await Task.Run(() =>
            {
                for (int i = 0; i < images.Count; i++)
                {
                    var each = settings with
                    {
                        Width = Math.Max(1, (int)Math.Round(images[i].Width * scaleX)),
                        Height = Math.Max(1, (int)Math.Round(images[i].Height * scaleY)),
                        Crop = i == pages.IndexOf(Doc) ? settings.Crop : null,
                    };
                    if (each.Crop != null) each = each with { Width = settings.Width, Height = settings.Height };
                    ImageExporter.Save(images[i], Path.Combine(folder, $"{name}-{i + 1}{extension}"), each);
                }
            });
            _main.SetStatus($"Exported {images.Count} pages to {folder}");
        });
    }

    /// <summary>Asks to save unsaved changes. Returns false if the user cancelled.</summary>
    public bool ConfirmDiscardChanges()
    {
        if (!Doc.Project.IsDirty) return true;
        return _main.Dialogs.AskSaveChanges(Doc.DisplayName) switch
        {
            Services.SaveChangesAnswer.Save => Save(saveAs: false),
            Services.SaveChangesAnswer.Discard => true,
            _ => false,
        };
    }
}
