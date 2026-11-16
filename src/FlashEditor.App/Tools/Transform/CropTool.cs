using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.App.Tools.Selecting;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Tools.Transform;

/// <summary>Crop shape presets (width : height). 0 = free.</summary>
public sealed record CropAspect(string Name, double Ratio)
{
    public override string ToString() => Name;
}

/// <summary>
/// Drag a rectangle, then press Enter (or click Apply) to crop the image to it. Esc cancels.
/// An aspect preset (square, 16:9, Instagram portrait...) keeps the box in that shape.
/// The tool object itself is shown in the options bar (Apply / Cancel buttons).
/// </summary>
public sealed class CropTool : ToolBase
{
    private Path? _shade;
    private PointD _start;
    private bool _dragging;
    private PixelRect _cropRect = PixelRect.Empty;
    private CropAspect _aspect;
    private bool _layerOnly;

    public CropTool(IToolHost host) : base(host)
    {
        ApplyCommand = new RelayCommand(Apply, () => HasCropRect);
        CancelCommand = new RelayCommand(ClearCrop, () => HasCropRect);
        _aspect = Aspects[0];
    }

    public static IReadOnlyList<CropAspect> Aspects { get; } = new[]
    {
        new CropAspect("Free", 0),
        new CropAspect("Square 1:1", 1),
        new CropAspect("Portrait 4:5 (Instagram)", 4.0 / 5),
        new CropAspect("Story 9:16", 9.0 / 16),
        new CropAspect("Wide 16:9", 16.0 / 9),
        new CropAspect("Photo 3:2", 3.0 / 2),
        new CropAspect("Photo 4:3", 4.0 / 3),
    };

    public CropAspect Aspect
    {
        get => _aspect;
        set
        {
            if (value == null || !SetField(ref _aspect, value)) return;
            if (HasCropRect) SetCropRect(Constrain(_cropRect, _cropRect.X, _cropRect.Y));
            else if (value.Ratio > 0) SetCropRect(LargestCentered(value.Ratio));
        }
    }

    /// <summary>What the crop applies to (shown in the options bar).</summary>
    public static IReadOnlyList<string> Targets { get; } = new[] { "Whole picture (canvas)", "Selected layer / object only" };

    /// <summary>0 = crop the whole picture, 1 = crop only the selected layer or object.</summary>
    public int TargetIndex
    {
        get => _layerOnly ? 1 : 0;
        set
        {
            bool layerOnly = value == 1;
            if (_layerOnly == layerOnly) return;
            _layerOnly = layerOnly;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeText));
            if (layerOnly) StartFromLayer(); else ClearCrop();
        }
    }

    /// <summary>Starts cropping just the selected layer / object (right-click > Crop This Object).</summary>
    public void CropActiveLayer()
    {
        _layerOnly = true;
        OnPropertyChanged(nameof(TargetIndex));
        StartFromLayer();
    }

    private void StartFromLayer()
    {
        if (TransformHandles.FrameOf(Document.ActiveLayer) is { } frame)
            SetCropRect(frame.Bounds().Intersect(Document.Bounds));
        Host.SetStatus("Drag a new box over the object, then press Enter (or Apply) to keep only that part of it.");
    }

    public override string Name => "Crop";
    public override string IconKey => "Crop";
    public override Key Shortcut => Key.C;
    public override object Options => this;

    public RelayCommand ApplyCommand { get; }
    public RelayCommand CancelCommand { get; }

    public bool HasCropRect => !_cropRect.IsEmpty;
    public string SizeText => HasCropRect ? $"{_cropRect.Width} x {_cropRect.Height} px"
        : _layerOnly ? "Drag over the object to choose the part to keep." : "Drag over the image to choose the area to keep.";

    public override void Activate()
    {
        // Start from the current selection, or (layer mode) from the selected object's box.
        if (Document.Selection is { } selection) SetCropRect(selection.Bounds);
        else if (_layerOnly) StartFromLayer();
    }

    public override void Deactivate() => ClearCrop();

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        if (e.ClickCount == 2 && HasCropRect) { Apply(); return; }
        _start = e.Position;
        _dragging = true;
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (!_dragging) return;
        var rect = PixelRect.FromCorners((int)Math.Round(_start.X), (int)Math.Round(_start.Y),
                                         (int)Math.Round(e.Position.X), (int)Math.Round(e.Position.Y));
        SetCropRect(Constrain(rect.Intersect(Document.Bounds), (int)Math.Round(_start.X), (int)Math.Round(_start.Y)));
    }

    public override void MouseUp(ToolMouseArgs e) => _dragging = false;

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Enter && HasCropRect) { Apply(); return true; }
        if (key == Key.Escape && HasCropRect) { ClearCrop(); return true; }
        return false;
    }

    private void Apply()
    {
        if (!HasCropRect) return;
        var rect = _cropRect;
        ClearCrop();
        if (_layerOnly)
        {
            CropLayer(rect);
            return;
        }
        // Reframe keeps shapes and text editable (and cropped-off pixels recoverable with Reveal All).
        Push(CanvasChangeAction.Reframe("Crop", Document, rect.Width, rect.Height, -rect.X, -rect.Y));
        Host.Canvas?.FitToWindow();
        Host.SetStatus($"Cropped to {rect.Width} x {rect.Height}");
    }

    /// <summary>Keeps only the part of the selected layer / object inside the box; the rest of the picture is untouched.</summary>
    private void CropLayer(PixelRect rect)
    {
        if (Document.ActiveLayer is not { } layer || !layer.IsVisible) return;
        if (layer.IsLocked)
        {
            Host.SetStatus($"\"{layer.Name}\" is locked. Unlock it (Ctrl+L) to crop it.");
            return;
        }
        var before = layer.SaveState();
        layer.RestoreState(LayerCrop.ToRectangle(layer, rect));
        Push(new LayerContentAction("Crop Layer", layer, before, layer.SaveState()));
        Host.SelectTool<MoveTool>();
        Host.SetStatus($"Cropped \"{layer.Name}\" to {rect.Width} x {rect.Height}. It is still a movable, resizable picture.");
    }

    /// <summary>Shrinks the rectangle to the chosen aspect, keeping the corner at (anchorX, anchorY).</summary>
    private PixelRect Constrain(PixelRect rect, int anchorX, int anchorY)
    {
        if (_aspect.Ratio <= 0 || rect.IsEmpty) return rect;
        int w = rect.Width, h = rect.Height;
        if (w / (double)h > _aspect.Ratio) w = Math.Max(1, (int)Math.Round(h * _aspect.Ratio));
        else h = Math.Max(1, (int)Math.Round(w / _aspect.Ratio));
        int x = rect.X == anchorX ? rect.X : rect.Right - w;
        int y = rect.Y == anchorY ? rect.Y : rect.Bottom - h;
        return new PixelRect(x, y, w, h);
    }

    private PixelRect LargestCentered(double ratio)
    {
        int w = Document.Width, h = Document.Height;
        if (w / (double)h > ratio) w = (int)Math.Round(h * ratio); else h = (int)Math.Round(w / ratio);
        return new PixelRect((Document.Width - w) / 2, (Document.Height - h) / 2, w, h);
    }

    private void SetCropRect(PixelRect rect)
    {
        _cropRect = rect;
        if (_shade == null)
        {
            _shade = new Path
            {
                Fill = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                Stroke = Brushes.White,
            };
            Host.Canvas?.AddOverlay(_shade);
        }
        // Darken everything outside the crop rectangle.
        var outside = new GeometryGroup { FillRule = FillRule.EvenOdd };
        outside.Children.Add(new RectangleGeometry(new Rect(0, 0, Document.Width, Document.Height)));
        outside.Children.Add(new RectangleGeometry(new Rect(rect.X, rect.Y, rect.Width, rect.Height)));
        _shade.Data = outside;
        OnPropertyChanged(nameof(HasCropRect));
        OnPropertyChanged(nameof(SizeText));
    }

    private void ClearCrop()
    {
        if (_shade != null) Host.Canvas?.RemoveOverlay(_shade);
        _shade = null;
        _cropRect = PixelRect.Empty;
        _dragging = false;
        OnPropertyChanged(nameof(HasCropRect));
        OnPropertyChanged(nameof(SizeText));
    }
}
