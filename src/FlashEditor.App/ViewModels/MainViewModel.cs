using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using FlashEditor.App.Services;
using FlashEditor.App.Tools;
using FlashEditor.App.ViewModels.Commands;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// Root view model of the main window. Owns the open document, colors, tools and the
/// menu command groups, and acts as the <see cref="IToolHost"/> for tools.
/// </summary>
public sealed class MainViewModel : ObservableObject, IToolHost
{
    private DocumentViewModel _document;
    private string _statusText = "Ready";
    private string _cursorText = "";
    private double _zoom = 1;
    private bool _isHistoryExpanded;
    private readonly HashSet<Layer> _selected = new();
    private bool _changingSelection;

    public MainViewModel(IDialogService dialogs, ClipboardService clipboard)
    {
        Dialogs = dialogs;
        Clipboard = clipboard;
        Colors = new ColorsViewModel(dialogs);
        _document = CreateDocumentViewModel(
            Core.Documents.Document.CreateBlank(1280, 720, ColorRgba.White), null, "New Image");
        Documents.Add(_document);
        Tools = new ToolsViewModel(this);
        SelectDocumentCommand = new Infrastructure.RelayCommand(p => { if (p is DocumentViewModel d) Document = d; });
        CloseDocumentCommand = new Infrastructure.RelayCommand(p => CloseDocument(p as DocumentViewModel ?? Document));
        NextDocumentCommand = new Infrastructure.RelayCommand(() => Cycle(+1), () => Documents.Count > 1);
        PreviousDocumentCommand = new Infrastructure.RelayCommand(() => Cycle(-1), () => Documents.Count > 1);
        RenameDocumentCommand = new Infrastructure.RelayCommand(p =>
        {
            var document = p as DocumentViewModel ?? Document;
            Document = document;
            document.IsRenaming = true;
        });

        File = new FileCommands(this);
        Edit = new EditCommands(this);
        Image = new ImageCommands(this);
        Layers = new LayerCommands(this);
        Effects = new EffectCommands(this);
        Upscale = new UpscaleCommands(this);
        View = new ViewCommands(this);
        Properties = new ObjectPropertiesViewModel(this);
        TrackSelection();
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Document)) TrackSelection(); };
    }

    // ------------------------------------------------------------------ multi-selection

    /// <summary>Raised when the set of selected layers changes (Shift+click, Ctrl+click in the Layers panel).</summary>
    public event EventHandler? SelectedLayersChanged;

    /// <summary>All selected layers, bottom to top. The active layer is always one of them.</summary>
    public IReadOnlyList<Layer> SelectedLayers
        => Document.Model.Layers.Where(l => _selected.Contains(l) || l == Document.Model.ActiveLayer).ToList();

    /// <summary>Selects just this layer.</summary>
    public void SelectOnly(Layer? layer)
    {
        _changingSelection = true;
        _selected.Clear();
        if (layer != null) _selected.Add(layer);
        Document.Model.ActiveLayer = layer ?? Document.Model.ActiveLayer;
        _changingSelection = false;
        SelectedLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds the layer to the selection, or removes it if it was selected (Shift+click).</summary>
    public void ToggleSelected(Layer layer)
    {
        _changingSelection = true;
        var doc = Document.Model;
        if (doc.ActiveLayer != null) _selected.Add(doc.ActiveLayer);
        if (_selected.Contains(layer) && _selected.Count > 1)
        {
            _selected.Remove(layer);
            if (doc.ActiveLayer == layer) doc.ActiveLayer = SelectedLayers.LastOrDefault(l => l != layer) ?? _selected.First();
        }
        else
        {
            _selected.Add(layer);
            doc.ActiveLayer = layer;
        }
        _changingSelection = false;
        SelectedLayersChanged?.Invoke(this, EventArgs.Empty);
        SetStatus(_selected.Count > 1 ? $"{_selected.Count} selected. Drag to move them together, Ctrl+G to group." : "");
    }

    /// <summary>Sets the selection from the Layers panel (Ctrl / Shift + click there).</summary>
    public void SetSelection(IEnumerable<Layer> layers, Layer? active)
    {
        _changingSelection = true;
        _selected.Clear();
        foreach (var l in layers) _selected.Add(l);
        if (active != null) { _selected.Add(active); Document.Model.ActiveLayer = active; }
        _changingSelection = false;
        SelectedLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    private Document? _selectionDocument;

    private void TrackSelection()
    {
        if (_selectionDocument != null)
        {
            _selectionDocument.PropertyChanged -= OnSelectionDocumentChanged;
            _selectionDocument.Layers.CollectionChanged -= OnSelectionLayersChanged;
        }
        _selectionDocument = Document.Model;
        _selectionDocument.PropertyChanged += OnSelectionDocumentChanged;
        _selectionDocument.Layers.CollectionChanged += OnSelectionLayersChanged;
        SelectOnly(_selectionDocument.ActiveLayer);
    }

    private void OnSelectionDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The active layer changed some other way (a click in the Layers panel, undo...): select just it.
        if (e.PropertyName == nameof(Core.Documents.Document.ActiveLayer) && !_changingSelection)
        {
            var active = Document.Model.ActiveLayer;
            if (active == null || !_selected.Contains(active)) SelectOnly(active);
            else SelectedLayersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSelectionLayersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _selected.RemoveWhere(l => !Document.Model.Layers.Contains(l));
        SelectedLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Settings of the selected shape / text / image (the Properties panel).</summary>
    public ObjectPropertiesViewModel Properties { get; }

    public Func<PixelBuffer, CancellationToken, float[]>? SubjectMask => Effects.SubjectMaskIfInstalled;

    /// <summary>History panel open or minimized (minimized by default).</summary>
    public bool IsHistoryExpanded
    {
        get => _isHistoryExpanded;
        set => SetField(ref _isHistoryExpanded, value);
    }

    public IDialogService Dialogs { get; }
    public ClipboardService Clipboard { get; }
    public ColorsViewModel Colors { get; }
    public ToolsViewModel Tools { get; }

    public FileCommands File { get; }
    public EditCommands Edit { get; }
    public ImageCommands Image { get; }
    public LayerCommands Layers { get; }
    public EffectCommands Effects { get; }
    public UpscaleCommands Upscale { get; }
    public ViewCommands View { get; }

    /// <summary>Set by the main window once the canvas control exists.</summary>
    public ICanvasView? Canvas { get; set; }

    /// <summary>All open pictures (one tab each).</summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = new();

    /// <summary>The picture shown in the editor (the active tab).</summary>
    public DocumentViewModel Document
    {
        get => _document;
        private set
        {
            if (value == null || value == _document) return;
            Tools.ResetActiveTool(); // finish half-done tool work on the old picture
            _document = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
            Infrastructure.RelayCommand.Refresh();
        }
    }

    public Infrastructure.RelayCommand SelectDocumentCommand { get; }
    public Infrastructure.RelayCommand CloseDocumentCommand { get; }
    public Infrastructure.RelayCommand NextDocumentCommand { get; }
    public Infrastructure.RelayCommand PreviousDocumentCommand { get; }
    public Infrastructure.RelayCommand RenameDocumentCommand { get; }

    private void Cycle(int direction)
    {
        int i = Documents.IndexOf(Document);
        Document = Documents[(i + direction + Documents.Count) % Documents.Count];
    }

    /// <summary>Shows an already open file instead of opening it twice. Returns true when found.</summary>
    public bool ActivateIfOpen(string path)
    {
        var open = Documents.FirstOrDefault(d => d.FilePath != null
            && string.Equals(System.IO.Path.GetFullPath(d.FilePath), System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (open == null) return false;
        Document = open;
        SetStatus($"{open.DisplayName} is already open.");
        return true;
    }

    /// <summary>Closes a tab, asking to save unsaved changes. Returns false when the user cancelled.</summary>
    public bool CloseDocument(DocumentViewModel document)
    {
        Document = document; // show it while asking
        if (!File.ConfirmDiscardChanges()) return false;

        int index = Documents.IndexOf(document);
        if (Documents.Count == 1)
        {
            var blank = CreateDocumentViewModel(Core.Documents.Document.CreateBlank(1280, 720, ColorRgba.White), null, "New Image");
            Documents.Add(blank);
        }
        Document = Documents[index + 1 < Documents.Count ? index + 1 : index - 1];
        Documents.Remove(document);
        document.PropertyChanged -= OnDocumentPropertyChanged;
        document.Dispose();
        return true;
    }

    /// <summary>Asks about every picture with unsaved changes (when the window closes). False = cancelled.</summary>
    public bool ConfirmCloseAll()
    {
        foreach (var document in Documents.Where(d => d.IsDirty).ToList())
        {
            Document = document;
            if (!File.ConfirmDiscardChanges()) return false;
        }
        return true;
    }

    public string Title => $"{Document.DisplayName}{(Document.IsDirty ? " *" : "")} - Flash Editor";

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>Mouse position shown in the status bar.</summary>
    public string CursorText
    {
        get => _cursorText;
        set => SetField(ref _cursorText, value);
    }

    /// <summary>Current zoom (1 = 100%), reported by the canvas.</summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (SetField(ref _zoom, value)) OnPropertyChanged(nameof(ZoomText));
        }
    }

    public string ZoomText => $"{Zoom * 100:0.#}%";

    public void SetStatus(string message) => StatusText = message;

    public void SelectTool<T>() where T : ToolBase => Tools.ActiveTool = Tools.Get<T>();

    /// <summary>
    /// Shows a newly created or opened picture in a new tab. An untouched blank picture is replaced
    /// instead of keeping a useless extra tab.
    /// </summary>
    public void LoadDocument(Document model, string? filePath, string initialStateName)
    {
        var replace = Documents.Count == 1 && Document.IsPristine ? Document : null;
        var document = CreateDocumentViewModel(model, filePath, initialStateName);
        Documents.Add(document);
        Document = document;
        if (replace != null)
        {
            Documents.Remove(replace);
            replace.PropertyChanged -= OnDocumentPropertyChanged;
            replace.Dispose();
        }
    }

    public void Push(IHistoryAction? action)
    {
        if (action != null) Document.Model.History.Push(action);
    }

    /// <summary>
    /// Gets the active layer if its pixels can be edited; otherwise shows a message.
    /// An editable object (shape, text, image) is first converted to pixels (one undo step).
    /// </summary>
    public bool TryGetEditableLayer(out Layer layer)
    {
        if (!TryGetVisibleLayer(out layer)) return false;
        if (IsLockedWithMessage(layer)) return false;
        ConvertToPixels(layer);
        return true;
    }

    /// <summary>True (and shows how to unlock) when the layer is locked.</summary>
    public bool IsLockedWithMessage(Layer layer)
    {
        if (!layer.IsLocked) return false;
        SetStatus($"\"{layer.Name}\" is locked. Unlock it with the lock icon in the Layers panel, right-click > Unlock, or Ctrl+L.");
        return true;
    }

    /// <summary>Gets the active layer if there is one and it is visible.</summary>
    public bool TryGetVisibleLayer(out Layer layer)
    {
        layer = Document.Model.ActiveLayer!;
        if (layer == null)
        {
            SetStatus("Select a layer first.");
            return false;
        }
        if (!layer.IsVisible)
        {
            Dialogs.ShowInfo("Layer hidden", "The active layer is hidden. Show it (eye icon in the Layers panel) to edit it.");
            return false;
        }
        return true;
    }

    /// <summary>Turns an object layer into a normal pixel layer (undoable). Does nothing for pixel layers.</summary>
    public void ConvertToPixels(Layer layer, bool announce = true)
    {
        if (layer.Object == null || IsLockedWithMessage(layer)) return;
        string kind = layer.Object.KindName.ToLowerInvariant();
        Push(ObjectChangeAction.Apply("Convert to Pixels", layer, null));
        if (announce)
            SetStatus($"\"{layer.Name}\" was a {kind} object and is now normal pixels so it can be painted on. Ctrl+Z makes it editable again.");
    }

    /// <summary>Adds a new layer holding <paramref name="obj"/> above the active layer and selects it.</summary>
    public Layer AddObjectLayer(LayerObject obj, string actionName)
    {
        var doc = Document.Model;
        var layer = new Layer(UniqueName(obj.SuggestedLayerName), doc.Width, doc.Height);
        layer.SetObject(obj);
        Push(LayerStructureAction.Record(actionName, doc, () =>
        {
            var layers = doc.Layers.ToList();
            layers.Insert(Math.Min(layers.Count, doc.ActiveLayerIndex + 1), layer);
            doc.SetLayers(layers, layer);
        }));
        return layer;
    }

    private string UniqueName(string name)
    {
        var names = Document.Model.Layers.Select(l => l.Name).ToHashSet();
        if (!names.Contains(name)) return name;
        for (int i = 2; ; i++)
            if (!names.Contains($"{name} {i}")) return $"{name} {i}";
    }

    /// <summary>
    /// The top-most visible layer with something at <paramref name="point"/> (an object's box, or a
    /// non-transparent pixel), or null. <paramref name="margin"/> widens object boxes (easier to hit thin lines).
    /// </summary>
    public Layer? FindLayerAt(PointD point, double margin = 0)
    {
        var doc = Document.Model;
        int x = (int)Math.Floor(point.X), y = (int)Math.Floor(point.Y);
        for (int i = doc.Layers.Count - 1; i >= 0; i--)
        {
            var layer = doc.Layers[i];
            if (!layer.IsVisible || layer.Opacity <= 0.02) continue;
            if (layer.Object is { } obj)
            {
                if (obj.Box.Contains(point, margin + (obj.IsLineLike ? 4 : 0))) return layer;
            }
            else if (doc.Bounds.Contains(x, y) && layer.Pixels.GetPixel(x, y).A > 16)
            {
                return layer;
            }
        }
        return null;
    }

    /// <summary>Makes the layer under the point active (Canva-style click to select). Returns it, or null.</summary>
    public Layer? SelectLayerAt(PointD point, double margin = 0)
    {
        var layer = FindLayerAt(point, margin);
        if (layer != null) Document.Model.ActiveLayer = layer;
        return layer;
    }

    /// <summary>Runs a possibly slow operation with a wait cursor and friendly error reporting.</summary>
    public void Run(string description, Action action)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Dialogs.ShowError($"{description} failed:\n\n{ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    public async void RunAsync(string description, Func<Task> action)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Dialogs.ShowError($"{description} failed:\n\n{ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private DocumentViewModel CreateDocumentViewModel(Document model, string? filePath, string initialStateName)
    {
        var vm = new DocumentViewModel(model, filePath, initialStateName);
        vm.PropertyChanged += OnDocumentPropertyChanged;
        return vm;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == Document && e.PropertyName is nameof(DocumentViewModel.IsDirty) or nameof(DocumentViewModel.DisplayName))
            OnPropertyChanged(nameof(Title));
    }
}
