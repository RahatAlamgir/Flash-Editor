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
        if (model.EffectiveMask != null) BitmapConverter.Update(_bitmap, model.Pixels, model.EffectiveMask, model.Pixels.Bounds);
        _iconPixels = Thumbnail.Create(model.Pixels, IconMaxWidth, IconMaxHeight);
        RefreshMaskIcon();
        _icon = BitmapConverter.CreateWriteable(_iconPixels);
        _iconTimer = new System.Windows.Threading.DispatcherTimer();
        _iconTimer.Tick += (_, _) => RefreshIcon();

        model.PropertyChanged += OnModelPropertyChanged;
        model.PixelsChanged += OnPixelsChanged;
        model.PixelsReplaced += OnPixelsReplaced;
        model.MaskChanged += OnMaskChanged;
        model.MaskReplaced += OnMaskReplaced;
        history.Changed += OnHistoryChanged;
        ToggleMaskCommand = new Infrastructure.RelayCommand(ToggleMask, () => Model.HasMask);
        RebuildChildren();
    }

    // ------------------------------------------------------------------ layer mask

    private WriteableBitmap? _maskIcon;

    /// <summary>True when the layer has a mask (a small picture of it is shown next to the layer's picture).</summary>
    public bool HasMask => Model.HasMask;

    /// <summary>False when the mask is switched off (kept, but ignored).</summary>
    public bool MaskEnabled => Model.MaskEnabled;

    /// <summary>The mask as a small grey picture (white = shows, black = hidden).</summary>
    public WriteableBitmap? MaskIcon => _maskIcon;

    /// <summary>Switches the mask on or off (one undo step).</summary>
    public Infrastructure.RelayCommand ToggleMaskCommand { get; }

    private void ToggleMask()
    {
        if (!Model.HasMask) return;
        _history.Push(MaskStateAction.Record(Model.MaskEnabled ? "Switch Mask Off" : "Switch Mask On", Model, () => Model.MaskEnabled = !Model.MaskEnabled));
    }

    private void RefreshMaskIcon()
    {
        if (Model.Mask is not { } mask)
        {
            _maskIcon = null;
            return;
        }
        var small = Thumbnail.Create(mask.ToGrayPicture(), IconMaxWidth, IconMaxHeight);
        if (_maskIcon == null || _maskIcon.PixelWidth != small.Width || _maskIcon.PixelHeight != small.Height) _maskIcon = BitmapConverter.CreateWriteable(small);
        else BitmapConverter.Update(_maskIcon, small, small.Bounds);
    }

    private void OnMaskChanged(object? sender, PixelRect region)
    {
        BitmapConverter.Update(_bitmap, Model.Pixels, Model.EffectiveMask, region);
        ScheduleIconRefresh(IconDelayIdle);
    }

    private void OnMaskReplaced(object? sender, EventArgs e)
    {
        BitmapConverter.Update(_bitmap, Model.Pixels, Model.EffectiveMask, Model.Pixels.Bounds);
        RefreshMaskIcon();
        OnPropertyChanged(nameof(HasMask));
        OnPropertyChanged(nameof(MaskEnabled));
        OnPropertyChanged(nameof(MaskIcon));
        Infrastructure.RelayCommand.Refresh();
        ScheduleIconRefresh(IconDelayIdle);
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
        if (Model.HasMask) { RefreshMaskIcon(); OnPropertyChanged(nameof(MaskIcon)); }

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

    /// <summary>Names for the blend mode list.</summary>
    public IReadOnlyList<string> BlendModeNames => BlendModes.Names;

    /// <summary>How the layer mixes with the layers below (the name shown in the list).</summary>
    public string BlendModeName
    {
        get => BlendModes.NameOf(Model.BlendMode);
        set
        {
            int index = BlendModes.Names.ToList().IndexOf(value ?? "");
            if (index < 0) return;
            var mode = BlendModes.All[index];
            if (mode == Model.BlendMode) return;
            var old = Model.BlendMode;
            Model.BlendMode = mode;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.BlendMode, old, mode));
        }
    }

    /// <summary>Tooltip that explains the chosen blend mode.</summary>
    public string BlendModeHelp => BlendModes.Describe(Model.BlendMode);

    // ------------------------------------------------------------------ adjustment layers

    /// <summary>True for a layer that changes the colors of the layers below it instead of having a picture.</summary>
    public bool IsAdjustment => Model.Adjustment != null;

    public double AdjustBrightness { get => Model.Adjustment?.Brightness ?? 0; set => Adjust(a => a with { Brightness = Math.Round(Math.Clamp(value, -100, 100)) }); }
    public double AdjustContrast { get => Model.Adjustment?.Contrast ?? 0; set => Adjust(a => a with { Contrast = Math.Round(Math.Clamp(value, -100, 100)) }); }
    public double AdjustSaturation { get => Model.Adjustment?.Saturation ?? 0; set => Adjust(a => a with { Saturation = Math.Round(Math.Clamp(value, -100, 100)) }); }
    public double AdjustHue { get => Model.Adjustment?.Hue ?? 0; set => Adjust(a => a with { Hue = Math.Round(Math.Clamp(value, -180, 180)) }); }

    // Levels
    public bool AdjustIsBasic => Model.Adjustment is { Kind: AdjustmentKind.Basic };
    public bool AdjustIsLevels => Model.Adjustment is { Kind: AdjustmentKind.Levels };
    public bool AdjustIsCurves => Model.Adjustment is { Kind: AdjustmentKind.Curves };

    public double LevelsBlack { get => Model.Adjustment?.LevelsBlack ?? 0; set => Adjust(a => a with { LevelsBlack = Math.Round(Math.Clamp(value, 0, Math.Min(254, a.LevelsWhite - 1))) }); }
    public double LevelsWhite { get => Model.Adjustment?.LevelsWhite ?? 255; set => Adjust(a => a with { LevelsWhite = Math.Round(Math.Clamp(value, Math.Max(1, a.LevelsBlack + 1), 255)) }); }
    public double LevelsGamma { get => Model.Adjustment?.LevelsGamma ?? 1; set => Adjust(a => a with { LevelsGamma = Math.Round(Math.Clamp(value, 0.1, 5), 2) }); }
    public double OutputBlack { get => Model.Adjustment?.OutputBlack ?? 0; set => Adjust(a => a with { OutputBlack = Math.Round(Math.Clamp(value, 0, Math.Min(254, a.OutputWhite - 1))) }); }
    public double OutputWhite { get => Model.Adjustment?.OutputWhite ?? 255; set => Adjust(a => a with { OutputWhite = Math.Round(Math.Clamp(value, Math.Max(1, a.OutputBlack + 1), 255)) }); }

    // Curves: 0 = all colors, 1 = red, 2 = green, 3 = blue
    private int _curveChannel;

    public IReadOnlyList<string> CurveChannels { get; } = new[] { "RGB (all colors)", "Red", "Green", "Blue" };

    public int CurveChannel
    {
        get => _curveChannel;
        set
        {
            if (!SetField(ref _curveChannel, Math.Clamp(value, 0, 3))) return;
            OnPropertyChanged(nameof(CurvePoints));
        }
    }

    /// <summary>The points of the curve of the chosen channel (a straight line when it was not changed).</summary>
    public IReadOnlyList<CurvePoint> CurvePoints
    {
        get
        {
            var a = Model.Adjustment;
            var points = _curveChannel switch { 1 => a?.CurveRed, 2 => a?.CurveGreen, 3 => a?.CurveBlue, _ => a?.Curve };
            return points == null || points.Count == 0 ? CurveMath.Straight : points;
        }
    }

    /// <summary>
    /// Sets the curve of the chosen channel. While a point is dragged this only shows the change (<paramref name="commit"/> false); when the
    /// mouse is released it is one undo step back to <paramref name="before"/>.
    /// </summary>
    public void SetCurve(IReadOnlyList<CurvePoint> points, bool commit, IReadOnlyList<CurvePoint>? before = null)
    {
        if (Model.Adjustment is not { } old) return;
        var stored = CurveMath.IsStraight(points) ? null : points;
        var updated = _curveChannel switch
        {
            1 => old with { CurveRed = stored },
            2 => old with { CurveGreen = stored },
            3 => old with { CurveBlue = stored },
            _ => old with { Curve = stored },
        };
        if (!commit)
        {
            if (_curveBefore == null) _curveBefore = old;
            Model.Adjustment = updated;
            return;
        }
        var original = _curveBefore ?? old;
        _curveBefore = null;
        Model.Adjustment = updated;
        if (!Equals(original, updated)) _history.Push(new LayerPropertyAction(Model, LayerProperty.Adjustment, original, updated));
    }

    private AdjustmentStyle? _curveBefore;

    /// <summary>Puts the curve of the chosen channel back to a straight line.</summary>
    public void ResetCurve() => SetCurve(CurveMath.Straight, commit: true);

    private void Adjust(Func<AdjustmentStyle, AdjustmentStyle> change)
    {
        if (Model.Adjustment is not { } old) return;
        var updated = change(old);
        if (updated == old) return;
        Model.Adjustment = updated;
        _history.Push(new LayerPropertyAction(Model, LayerProperty.Adjustment, old, updated));
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
            case nameof(Layer.BlendMode): OnPropertyChanged(nameof(BlendModeName)); OnPropertyChanged(nameof(BlendModeHelp)); break;
            case nameof(Layer.Adjustment):
                OnPropertyChanged(nameof(IsAdjustment));
                OnPropertyChanged(nameof(AdjustBrightness));
                OnPropertyChanged(nameof(AdjustContrast));
                OnPropertyChanged(nameof(AdjustSaturation));
                OnPropertyChanged(nameof(AdjustHue));
                OnPropertyChanged(nameof(AdjustIsBasic));
                OnPropertyChanged(nameof(AdjustIsLevels));
                OnPropertyChanged(nameof(AdjustIsCurves));
                OnPropertyChanged(nameof(LevelsBlack));
                OnPropertyChanged(nameof(LevelsWhite));
                OnPropertyChanged(nameof(LevelsGamma));
                OnPropertyChanged(nameof(OutputBlack));
                OnPropertyChanged(nameof(OutputWhite));
                OnPropertyChanged(nameof(CurvePoints));
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
        BitmapConverter.Update(_bitmap, Model.Pixels, Model.EffectiveMask, region);
        ScheduleIconRefresh(IconDelayIdle);
    }

    private void OnPixelsReplaced(object? sender, EventArgs e)
    {
        ScheduleIconRefresh(IconDelayIdle);
        if (_bitmap.PixelWidth == Model.Width && _bitmap.PixelHeight == Model.Height)
        {
            BitmapConverter.Update(_bitmap, Model.Pixels, Model.EffectiveMask, Model.Pixels.Bounds);
            return;
        }
        _bitmap = BitmapConverter.CreateWriteable(Model.Pixels);
        if (Model.EffectiveMask != null) BitmapConverter.Update(_bitmap, Model.Pixels, Model.EffectiveMask, Model.Pixels.Bounds);
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
        Model.MaskChanged -= OnMaskChanged;
        Model.MaskReplaced -= OnMaskReplaced;
    }
}
