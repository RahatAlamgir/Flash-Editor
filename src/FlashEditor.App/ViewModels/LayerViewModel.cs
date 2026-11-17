using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// Screen-side wrapper of a <see cref="Layer"/>: keeps a WPF bitmap in sync with the layer pixels and
/// records name / opacity / visibility changes made in the UI as undoable steps.
/// </summary>
public sealed class LayerViewModel : ObservableObject, IEditableName, IDisposable
{
    // The little icon in the Layers panel is NOT the canvas bitmap scaled down: it is its own small picture, redrawn
    // only after the user stopped changing the layer (see ScheduleIconRefresh). Redrawing it on every mouse move while
    // an object is dragged made the whole program sluggish.
    private const int IconMaxWidth = 96, IconMaxHeight = 68;
    private static readonly TimeSpan IconDelayAfterStep = TimeSpan.FromMilliseconds(200); // after a finished change (mouse up, undo...)
    private static readonly TimeSpan IconDelayIdle = TimeSpan.FromMilliseconds(1200);     // fallback for changes without an undo step

    private readonly HistoryManager _history;
    private WriteableBitmap _bitmap;
    private PixelBuffer _iconPixels;
    private WriteableBitmap _icon;
    private readonly System.Windows.Threading.DispatcherTimer _iconTimer;
    private bool _iconDirty;
    private bool _isEditingName;
    private bool _isExpanded = true;
    private bool _isGroupShown;
    private IReadOnlyList<GroupMember>? _shownMembers;

    public LayerViewModel(Layer model, HistoryManager history)
    {
        Model = model;
        _history = history;
        _bitmap = BitmapConverter.CreateWriteable(model.Pixels);
        _iconPixels = Thumbnail.Create(model.Pixels, IconMaxWidth, IconMaxHeight);
        _icon = BitmapConverter.CreateWriteable(_iconPixels);
        _iconTimer = new System.Windows.Threading.DispatcherTimer();
        _iconTimer.Tick += (_, _) => RefreshIcon();

        model.PropertyChanged += OnModelPropertyChanged;
        model.PixelsChanged += OnPixelsChanged;
        model.PixelsReplaced += OnPixelsReplaced;
        history.Changed += OnHistoryChanged;
        RebuildChildren();
    }

    /// <summary>The small picture next to the layer's name. Updated after a change is finished, not while it happens.</summary>
    public WriteableBitmap Icon => _icon;

    private void ScheduleIconRefresh(TimeSpan delay, bool onlyIfDirty = false)
    {
        if (onlyIfDirty && !_iconDirty) return;
        _iconDirty = true;
        _iconTimer.Stop();
        _iconTimer.Interval = delay;
        _iconTimer.Start();
    }

    private void OnHistoryChanged(object? sender, EventArgs e) => ScheduleIconRefresh(IconDelayAfterStep, onlyIfDirty: true);

    /// <summary>Redraws the icon (and the group's part rows) from the layer's current content.</summary>
    private void RefreshIcon()
    {
        _iconTimer.Stop();
        if (!_iconDirty) return;
        _iconDirty = false;

        var (w, h) = Thumbnail.FitSize(Model.Width, Model.Height, IconMaxWidth, IconMaxHeight);
        if (w != _iconPixels.Width || h != _iconPixels.Height)
        {
            _iconPixels = new PixelBuffer(w, h);
            _icon = BitmapConverter.CreateWriteable(_iconPixels);
            Thumbnail.Fill(Model.Pixels, _iconPixels);
            BitmapConverter.Update(_icon, _iconPixels, _iconPixels.Bounds);
            OnPropertyChanged(nameof(Icon));
        }
        else
        {
            Thumbnail.Fill(Model.Pixels, _iconPixels);
            BitmapConverter.Update(_icon, _iconPixels, _iconPixels.Bounds);
        }
        RebuildChildren();
    }

    /// <summary>The parts of a group, top part first (empty for any other layer). Shown indented under the group.</summary>
    public ObservableCollection<GroupChildViewModel> Children { get; } = new();

    public bool IsGroup => Model.Object is GroupObject;

    /// <summary>True when the group's parts are shown under it (the arrow in the Layers panel).</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value)) OnPropertyChanged(nameof(ShowChildren));
        }
    }

    public bool ShowChildren => IsGroup && IsExpanded;

    /// <summary>Re-creates the part rows, but only when the group's parts really changed (not when it is just moved).</summary>
    private void RebuildChildren()
    {
        var members = (Model.Object as GroupObject)?.Members;
        bool isGroup = members != null;
        if (isGroup != _isGroupShown)
        {
            _isGroupShown = isGroup;
            OnPropertyChanged(nameof(IsGroup));
            OnPropertyChanged(nameof(ShowChildren));
        }
        if (ReferenceEquals(members, _shownMembers)) return;
        _shownMembers = members;

        Children.Clear();
        if (members != null)
        {
            for (int i = members.Count - 1; i >= 0; i--) // top part first, like the layer list
                Children.Add(new GroupChildViewModel(Model, _history, i, members[i]));
        }
    }

    public Layer Model { get; }

    /// <summary>Displayed on the canvas and as the thumbnail in the Layers panel.</summary>
    public WriteableBitmap Bitmap => _bitmap;

    public int Width => Model.Width;
    public int Height => Model.Height;

    public string Name
    {
        get => Model.Name;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? Model.Name : value.Trim();
            if (value == Model.Name) return;
            var old = Model.Name;
            Model.Name = value;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Name, old, value));
        }
    }

    /// <summary>Layer opacity as a percentage (0..100) for the slider.</summary>
    public double OpacityPercent
    {
        get => Math.Round(Model.Opacity * 100);
        set
        {
            double newValue = Math.Clamp(value, 0, 100) / 100.0;
            if (Math.Abs(newValue - Model.Opacity) < 0.0001) return;
            double old = Model.Opacity;
            Model.Opacity = newValue;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Opacity, old, newValue));
        }
    }

    /// <summary>Opacity 0..1 for the canvas Image element.</summary>
    public double Opacity => Model.Opacity;

    public bool IsVisible
    {
        get => Model.IsVisible;
        set
        {
            if (value == Model.IsVisible) return;
            Model.IsVisible = value;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Visibility, !value, value));
        }
    }

    /// <summary>Locked layers cannot be moved or edited (toggle with the lock icon).</summary>
    public bool IsLocked
    {
        get => Model.IsLocked;
        set
        {
            if (value == Model.IsLocked) return;
            Model.IsLocked = value;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Lock, !value, value));
        }
    }

    /// <summary>True while the name is being edited inline in the Layers panel.</summary>
    public bool IsEditingName
    {
        get => _isEditingName;
        set => SetField(ref _isEditingName, value);
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Layer.Name): OnPropertyChanged(nameof(Name)); break;
            case nameof(Layer.Opacity):
                OnPropertyChanged(nameof(Opacity));
                OnPropertyChanged(nameof(OpacityPercent));
                break;
            case nameof(Layer.IsVisible): OnPropertyChanged(nameof(IsVisible)); break;
            case nameof(Layer.IsLocked): OnPropertyChanged(nameof(IsLocked)); break;
            case nameof(Layer.Object):
                // Only the cheap "is it a group" flag now; the part rows and the icon follow once the change is finished.
                bool isGroup = Model.Object is GroupObject;
                if (isGroup != _isGroupShown)
                {
                    _isGroupShown = isGroup;
                    OnPropertyChanged(nameof(IsGroup));
                    OnPropertyChanged(nameof(ShowChildren));
                }
                ScheduleIconRefresh(IconDelayIdle);
                break;
        }
    }

    private void OnPixelsChanged(object? sender, PixelRect region)
    {
        BitmapConverter.Update(_bitmap, Model.Pixels, region);
        ScheduleIconRefresh(IconDelayIdle);
    }

    private void OnPixelsReplaced(object? sender, EventArgs e)
    {
        ScheduleIconRefresh(IconDelayIdle);
        if (_bitmap.PixelWidth == Model.Width && _bitmap.PixelHeight == Model.Height)
        {
            BitmapConverter.Update(_bitmap, Model.Pixels, Model.Pixels.Bounds);
            return;
        }
        _bitmap = BitmapConverter.CreateWriteable(Model.Pixels);
        OnPropertyChanged(nameof(Bitmap));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
    }

    public void Dispose()
    {
        _iconTimer.Stop();
        _history.Changed -= OnHistoryChanged;
        Model.PropertyChanged -= OnModelPropertyChanged;
        Model.PixelsChanged -= OnPixelsChanged;
        Model.PixelsReplaced -= OnPixelsReplaced;
    }
}
