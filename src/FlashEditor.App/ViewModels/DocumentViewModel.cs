using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.IO;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// Screen-side state of the open image: layer view models (for the canvas and Layers panel),
/// history rows, file name and "unsaved changes" flag.
/// </summary>
public sealed class DocumentViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<Layer, LayerViewModel> _layerViewModels = new();
    private readonly string _initialStateName;
    private HistoryItemViewModel? _selectedHistoryItem;
    private bool _syncingHistory;
    private string? _filePath;
    private string? _customName;
    private bool _isRenaming;

    public DocumentViewModel(Document model, string? filePath, string initialStateName)
    {
        Model = model;
        _filePath = filePath;
        _initialStateName = initialStateName;
        FileFormat = filePath == null ? null : ImageFileFormats.FromPath(filePath);

        model.Layers.CollectionChanged += OnLayersChanged;
        model.PropertyChanged += OnModelPropertyChanged;
        model.History.Changed += OnHistoryChanged;
        RebuildLayers();
        RebuildHistory();
    }

    public Document Model { get; }

    /// <summary>Bottom to top: the canvas draws them in this order.</summary>
    public ObservableCollection<LayerViewModel> Layers { get; } = new();

    /// <summary>Top to bottom: the order shown in the Layers panel.</summary>
    public ObservableCollection<LayerViewModel> LayersTopDown { get; } = new();

    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = new();

    public int Width => Model.Width;
    public int Height => Model.Height;

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetField(ref _filePath, value))
            {
                CustomName = null; // after saving under a file name, the tab shows that name
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(TabTitle));
                OnPropertyChanged(nameof(TabToolTip));
            }
        }
    }

    /// <summary>Format used by "Save" (null = ask with Save As).</summary>
    public ImageFileFormat? FileFormat { get; set; }
    public int SaveQuality { get; set; } = 90;

    public string DisplayName => CustomName ?? (FilePath == null ? "Untitled" : Path.GetFileName(FilePath));

    /// <summary>A name the user gave the tab (null = use the file name).</summary>
    public string? CustomName
    {
        get => _customName;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (!SetField(ref _customName, value)) return;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(TabTitle));
            OnPropertyChanged(nameof(EditableName));
        }
    }

    /// <summary>The name shown in the rename box.</summary>
    public string EditableName
    {
        get => DisplayName;
        set { if (value?.Trim() != DisplayName) CustomName = value; }
    }

    /// <summary>True while the tab's name is being edited (double-click the tab).</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetField(ref _isRenaming, value);
    }
    public bool IsDirty => Model.History.IsDirty;

    /// <summary>Text on the document's tab: file name, with * when there are unsaved changes.</summary>
    public string TabTitle => DisplayName + (IsDirty ? " *" : "");

    /// <summary>Full path (or "not saved yet") for the tab's tooltip.</summary>
    public string TabToolTip => (FilePath ?? "Not saved yet") + "\nDouble-click to rename";

    /// <summary>A blank picture nobody has touched yet (opening a file replaces it instead of adding a tab).</summary>
    public bool IsPristine => FilePath == null && !Model.History.CanUndo && !Model.History.CanRedo;

    /// <summary>Zoom and pan of this document, remembered while another tab is shown (null = fit to window).</summary>
    public double? ViewZoom { get; set; }

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

    /// <summary>Selecting a row in the History panel jumps to that state.</summary>
    public HistoryItemViewModel? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set
        {
            if (_syncingHistory || value == null || value == _selectedHistoryItem) return;
            Model.History.JumpTo(value.Position);
        }
    }

    public LayerViewModel? FindViewModel(Layer layer) => _layerViewModels.GetValueOrDefault(layer);

    private void OnLayersChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildLayers();

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
            case nameof(Document.Width): OnPropertyChanged(nameof(Width)); break;
            case nameof(Document.Height): OnPropertyChanged(nameof(Height)); break;
        }
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        RebuildHistory();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(TabTitle));
        RelayCommand.Refresh();
    }

    private void RebuildHistory()
    {
        _syncingHistory = true;
        try
        {
            var history = Model.History;
            HistoryItems.Clear();
            HistoryItems.Add(new HistoryItemViewModel(_initialStateName, 0, false));
            for (int i = 0; i < history.Actions.Count; i++)
                HistoryItems.Add(new HistoryItemViewModel(history.Actions[i].Name, i + 1, i >= history.Position));

            _selectedHistoryItem = HistoryItems[history.Position];
            OnPropertyChanged(nameof(SelectedHistoryItem));
        }
        finally
        {
            _syncingHistory = false;
        }
    }

    public void Dispose()
    {
        Model.Layers.CollectionChanged -= OnLayersChanged;
        Model.PropertyChanged -= OnModelPropertyChanged;
        Model.History.Changed -= OnHistoryChanged;
        foreach (var vm in _layerViewModels.Values) vm.Dispose();
        _layerViewModels.Clear();
    }
}
