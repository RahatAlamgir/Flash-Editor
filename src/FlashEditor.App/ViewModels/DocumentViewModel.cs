using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.IO;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// Screen-side state of the open image: layer view models (for the canvas and Layers panel),
/// history rows, file name and "unsaved changes" flag.
/// </summary>
public sealed class DocumentViewModel : ObservableObject, IRenamable, IDisposable
{
    private readonly Dictionary<Layer, LayerViewModel> _layerViewModels = new();
    private string? _filePath;
    private string? _customName;
    private bool _isRenaming;

    public DocumentViewModel(Document model, string? filePath, string initialStateName)
    {
        Model = model;
        _filePath = filePath;
        FileFormat = filePath == null ? null : ImageFileFormats.FromPath(filePath);
        Project = new ProjectGroup { InitialStateName = initialStateName };   // every picture is a page of a project (a single picture is a project of one page)
        Project.Pages.Add(this);

        model.Layers.CollectionChanged += OnLayersChanged;
        model.PropertyChanged += OnModelPropertyChanged;
        model.History.Changed += OnHistoryChanged;
        RebuildLayers();
    }

    // ------------------------------------------------------------------ page picture (for the page strip and the grid view)

    private const int PageThumbnailWidth = 280, PageThumbnailHeight = 190;
    private System.Windows.Media.ImageSource? _pageThumbnail;
    private System.Windows.Threading.DispatcherTimer? _thumbnailTimer;

    /// <summary>A small picture of the whole page. It is made the first time it is needed and redrawn a moment after the page changed.</summary>
    public System.Windows.Media.ImageSource PageThumbnail => _pageThumbnail ??= MakePageThumbnail();

    private System.Windows.Media.ImageSource MakePageThumbnail()
    {
        var small = Core.Imaging.Thumbnail.Create(Core.Painting.Compositor.Flatten(Model), PageThumbnailWidth, PageThumbnailHeight);
        return Rendering.BitmapConverter.ToFrozenBitmap(small);
    }

    private void ScheduleThumbnail()
    {
        if (_pageThumbnail == null) return;                  // nobody looked at it yet
        _thumbnailTimer ??= new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(350),
        };
        _thumbnailTimer.Tick -= OnThumbnailTimer;
        _thumbnailTimer.Tick += OnThumbnailTimer;
        _thumbnailTimer.Stop();
        _thumbnailTimer.Start();
    }

    private void OnThumbnailTimer(object? sender, EventArgs e)
    {
        _thumbnailTimer?.Stop();
        _pageThumbnail = MakePageThumbnail();
        OnPropertyChanged(nameof(PageThumbnail));
    }

    /// <summary>"1280 x 720 px" for the grid view.</summary>
    public string SizeText => $"{Model.Width} x {Model.Height} px";

    public Document Model { get; }

    /// <summary>Bottom to top: the canvas draws them in this order.</summary>
    public ObservableCollection<LayerViewModel> Layers { get; } = new();

    /// <summary>Top to bottom: the order shown in the Layers panel.</summary>
    public ObservableCollection<LayerViewModel> LayersTopDown { get; } = new();

    public int Width => Model.Width;
    public int Height => Model.Height;

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetField(ref _filePath, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(TabToolTip));
                Project?.Refresh();
            }
        }
    }

    /// <summary>Format used by "Save" (null = ask with Save As).</summary>
    public ImageFileFormat? FileFormat { get; set; }
    public int SaveQuality { get; set; } = 90;

    /// <summary>The name of the project (used in the window title and in questions like "Save changes to ...").</summary>
    public string DisplayName => Project.Name;

    /// <summary>The name of this page: the one the user typed, else "Page 1", "Page 2"... by position in the project.</summary>
    public string PageTitle => CustomName ?? $"Page {Math.Max(0, Project.Pages.IndexOf(this)) + 1}";

    /// <summary>A name the user gave the page (null = "Page 1", "Page 2"...).</summary>
    public string? CustomName
    {
        get => _customName;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (!SetField(ref _customName, value)) return;
            RefreshTitle();
        }
    }

    /// <summary>The name shown in the rename box.</summary>
    public string EditableName
    {
        get => PageTitle;
        set { if (value?.Trim() != PageTitle) CustomName = value; }
    }

    private bool _isPageSelected;

    /// <summary>True when the page is picked in the page strip (Ctrl+click and Shift+click pick several); copy, duplicate and delete then work on all picked pages.</summary>
    public bool IsPageSelected
    {
        get => _isPageSelected;
        set => SetField(ref _isPageSelected, value);
    }

    /// <summary>True while the tab's name is being edited (double-click the tab).</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetField(ref _isRenaming, value);
    }
    public bool IsDirty => Model.History.IsDirty || Project?.StructureDirty == true || IsRecovered;

    /// <summary>True for a picture brought back after a crash: it counts as unsaved until the user saves it.</summary>
    public bool IsRecovered { get; set; }

    /// <summary>The pages this picture belongs to, or null when it is a single picture.</summary>
    /// <summary>The project this page belongs to (a single picture is a project of one page). Its steps go into the project's one history.</summary>
    public ProjectGroup Project
    {
        get => _project;
        set
        {
            if (ReferenceEquals(_project, value)) return;
            _project = value;
            Model.History.JoinProject(value?.History);
        }
    }
    private ProjectGroup _project = null!;

    /// <summary>Tells the tab that its unsaved-changes mark may have changed.</summary>
    public void RefreshDirty()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(TabTitle));
        Project?.Refresh();
    }

    /// <summary>Text on the document's tab: file name, with * when there are unsaved changes.</summary>
    /// <summary>True when this page has changes that were not saved yet.</summary>
    public bool IsPageDirty => Model.History.IsDirty || IsRecovered;

    /// <summary>Text on the page's tab: its name, with * when it has unsaved changes.</summary>
    public string TabTitle => PageTitle + (IsPageDirty ? " *" : "");

    /// <summary>The page name may have changed (renamed, or another page was added / removed before it).</summary>
    public void RefreshTitle()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(EditableName));
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(DisplayName));
    }

    /// <summary>Full path (or "not saved yet") for the tab's tooltip.</summary>
    public string TabToolTip => PageTitle + "\nDouble-click to rename";

    /// <summary>A blank picture nobody has touched yet (opening a file replaces it instead of adding a tab).</summary>
    public bool IsPristine => FilePath == null && !IsRecovered && Project.Pages.Count == 1 && !Model.History.CanUndo && !Model.History.CanRedo;

    /// <summary>Zoom and pan of this document, remembered while another tab is shown (null = fit to window).</summary>
    public double? ViewZoom { get; set; }

    /// <summary>True when the picture was shown "fit to window" (it then follows the window size again when its tab is shown).</summary>
    public bool ViewFitted { get; set; }

    /// <summary>Guide lines pulled out of the rulers: x positions of vertical ones, y positions of horizontal ones (not saved in the file).</summary>
    public List<double> VerticalGuides { get; } = new();
    public List<double> HorizontalGuides { get; } = new();
    public event EventHandler? GuidesChanged;
    public void NotifyGuidesChanged() => GuidesChanged?.Invoke(this, EventArgs.Empty);
    public void ClearGuides() { VerticalGuides.Clear(); HorizontalGuides.Clear(); NotifyGuidesChanged(); }
    public System.Windows.Vector ViewOffset { get; set; }

    public LayerViewModel? ActiveLayer
    {
        get => Model.ActiveLayer == null ? null : _layerViewModels.GetValueOrDefault(Model.ActiveLayer);
        set
        {
            if (value != null && Model.Layers.Contains(value.Model))
                Model.ActiveLayer = value.Model;
        }
    }

    public LayerViewModel? FindViewModel(Layer layer) => _layerViewModels.GetValueOrDefault(layer);

    private void OnLayersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildLayers();
        ScheduleThumbnail();
    }

    /// <summary>Re-creates the layer lists, reusing existing view models (and their bitmaps).</summary>
    private void RebuildLayers()
    {
        var current = Model.Layers.ToList();
        foreach (var stale in _layerViewModels.Keys.Except(current).ToList())
        {
            // Keep view models of removed layers alive only while undo may bring them back:
            // re-creating a bitmap is cheap, so simply drop them.
            _layerViewModels[stale].Dispose();
            _layerViewModels.Remove(stale);
        }

        Layers.Clear();
        LayersTopDown.Clear();
        foreach (var layer in current)
        {
            if (!_layerViewModels.TryGetValue(layer, out var vm))
            {
                vm = new LayerViewModel(layer, Model.History);
                _layerViewModels[layer] = vm;
            }
            Layers.Add(vm);
            LayersTopDown.Insert(0, vm);
        }
        OnPropertyChanged(nameof(ActiveLayer));
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Document.ActiveLayer): OnPropertyChanged(nameof(ActiveLayer)); break;
            case nameof(Document.Width): OnPropertyChanged(nameof(Width)); OnPropertyChanged(nameof(SizeText)); ScheduleThumbnail(); break;
            case nameof(Document.Height): OnPropertyChanged(nameof(Height)); OnPropertyChanged(nameof(SizeText)); ScheduleThumbnail(); break;
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        ScheduleThumbnail();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(TabTitle));
        Project?.Refresh();
        RelayCommand.Refresh();
    }

    public void Dispose()
    {
        Model.Layers.CollectionChanged -= OnLayersChanged;
        Model.PropertyChanged -= OnModelPropertyChanged;
        Model.History.Changed -= OnHistoryChanged;
        _thumbnailTimer?.Stop();
        foreach (var vm in _layerViewModels.Values) vm.Dispose();
        _layerViewModels.Clear();
    }
}
