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
/// Crop works like a selection tool: drag a box over the picture, then adjust it with the mouse (drag its corners and edges to resize it,
/// drag inside it to move it, drag outside it to draw a new one). Enter (or the Crop button) cuts the box out of the selected layer into a
/// new layer and hides the original layer, which is never changed. The canvas is never resized. Esc cancels. An aspect preset
/// (square, 16:9...) keeps the box in that shape.
/// </summary>
public sealed class CropTool : ToolBase
{
    private enum Drag { None, New, Move, Resize }

    private Path? _shade;
    private Path? _handles;
    private PixelRect _cropRect = PixelRect.Empty;
    private CropAspect _aspect;
    private Layer? _target;
    private Drag _drag;
    private HandleKind _handle;
    private PointD _start;
    private PixelRect _startRect;

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
            if (HasCropRect) SetCropRect(FitAspect(_cropRect));
        }
    }

    /// <summary>Starts cropping the selected layer / object (right-click > Crop This Object).</summary>
    public void CropActiveLayer() => StartFromLayer();

    private void StartFromLayer()
    {
        ClearCrop();
        var layer = Document.ActiveLayer;
        if (layer == null || !layer.IsVisible || layer.IsAdjustment)
        {
            Host.SetStatus("Select the layer or object you want to crop first.");
            return;
        }
        if (TransformHandles.FrameOf(layer) is not { } frame)
        {
            Host.SetStatus($"\"{layer.Name}\" is empty, there is nothing to crop.");
            return;
        }
        var bounds = frame.Bounds().Intersect(Document.Bounds);
        if (bounds.IsEmpty)
        {
            Host.SetStatus($"\"{layer.Name}\" is outside the picture.");
            return;
        }
        _target = layer;
        SetCropRect(FitAspect(bounds));
        Host.SetStatus($"Cropping \"{layer.Name}\": drag the corners and edges to adjust the box, then press Enter. Esc cancels. The canvas keeps its size.");
    }

    public override string Name => "Crop";
    public override string IconKey => "Crop";
    public override Key Shortcut => Key.C;
    public override object Options => this;
    public override Cursor Cursor => Cursors.Cross;

    public RelayCommand ApplyCommand { get; }
    public RelayCommand CancelCommand { get; }

    public bool HasCropRect => !_cropRect.IsEmpty;
    public string SizeText => HasCropRect ? $"{_cropRect.Width} x {_cropRect.Height} px"
        : "Drag a box over the picture.";

    public override void Activate()
    {
        ClearCrop();
        Host.SetStatus(Document.ActiveLayer == null
            ? "Crop: select a layer, then drag a box over the part you want to keep."
            : $"Crop: drag a box over the part of \"{Document.ActiveLayer.Name}\" you want to keep. Adjust it with the mouse, then press Enter.");
    }

    public override void Deactivate() => ClearCrop();

    // ------------------------------------------------------------------ mouse

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        if (e.ClickCount == 2 && Contains(_cropRect, e.Position)) { Apply(); return; }

        _start = e.Position;
        _startRect = _cropRect;
        _drag = Drag.None;
        double tolerance = 9 / Math.Max(0.01, Host.Canvas?.Zoom ?? 1);
        if (!HasCropRect) _drag = Drag.New;
        else if (HitHandle(e.Position, tolerance) is { } hit) { _drag = Drag.Resize; _handle = hit; }
        else if (Contains(_cropRect, e.Position)) _drag = Drag.Move;
        else _drag = Drag.New;
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_drag == Drag.None) return;
        var bounds = Document.Bounds;
        switch (_drag)
        {
            case Drag.Move:
            {
                int dx = (int)Math.Round(e.Position.X - _start.X), dy = (int)Math.Round(e.Position.Y - _start.Y);
                int x = Math.Clamp(_startRect.X + dx, bounds.X, Math.Max(bounds.X, bounds.Right - _startRect.Width));
                int y = Math.Clamp(_startRect.Y + dy, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - _startRect.Height));
                SetCropRect(new PixelRect(x, y, _startRect.Width, _startRect.Height));
                break;
            }
            case Drag.New:
            {
                var rect = PixelRect.FromCorners((int)Math.Round(_start.X), (int)Math.Round(_start.Y), (int)Math.Round(e.Position.X), (int)Math.Round(e.Position.Y)).Intersect(bounds);
                if (rect.IsEmpty) break;
                SetCropRect(ConstrainNew(rect, (int)Math.Round(_start.X), (int)Math.Round(_start.Y)));
                break;
            }
            case Drag.Resize:
                Resize(e.Position);
                break;
        }
    }

    public override void MouseUp(ToolMouseArgs e) => _drag = Drag.None;

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Enter && HasCropRect) { Apply(); return true; }
        if (key == Key.Escape && HasCropRect) { ClearCrop(); Host.SetStatus("Crop cancelled."); return true; }
        return false;
    }

    private static bool Contains(PixelRect r, PointD p) => p.X >= r.X && p.X <= r.Right && p.Y >= r.Y && p.Y <= r.Bottom;

    private HandleKind? HitHandle(PointD p, double tolerance)
    {
        var r = _cropRect;
        double l = r.X, t = r.Y, rr = r.Right, b = r.Bottom, cx = (l + rr) / 2, cy = (t + b) / 2;
        (HandleKind Kind, double X, double Y)[] spots =
        {
            (HandleKind.NW, l, t), (HandleKind.NE, rr, t), (HandleKind.SE, rr, b), (HandleKind.SW, l, b),
            (HandleKind.N, cx, t), (HandleKind.S, cx, b), (HandleKind.W, l, cy), (HandleKind.E, rr, cy),
        };
        foreach (var (kind, x, y) in spots)
            if (Math.Abs(p.X - x) <= tolerance && Math.Abs(p.Y - y) <= tolerance) return kind;
        // The edges themselves also grab (a thin strip), but only when the box is big enough on screen to tell the edge from the inside.
        bool nearX = p.Y >= t - tolerance && p.Y <= b + tolerance, nearY = p.X >= l - tolerance && p.X <= rr + tolerance;
        if (nearX && Math.Abs(p.X - l) <= tolerance / 2) return HandleKind.W;
        if (nearX && Math.Abs(p.X - rr) <= tolerance / 2) return HandleKind.E;
        if (nearY && Math.Abs(p.Y - t) <= tolerance / 2) return HandleKind.N;
        if (nearY && Math.Abs(p.Y - b) <= tolerance / 2) return HandleKind.S;
        return null;
    }

    private void Resize(PointD p)
    {
        var (sx, sy) = TransformHandles.Direction(_handle);
        var bounds = Document.Bounds;
        int left = _startRect.X, top = _startRect.Y, right = _startRect.Right, bottom = _startRect.Bottom;
        int px = (int)Math.Round(Math.Clamp(p.X, bounds.X, bounds.Right)), py = (int)Math.Round(Math.Clamp(p.Y, bounds.Y, bounds.Bottom));
        if (sx < 0) left = Math.Min(px, right - 1); else if (sx > 0) right = Math.Max(px, left + 1);
        if (sy < 0) top = Math.Min(py, bottom - 1); else if (sy > 0) bottom = Math.Max(py, top + 1);

        if (_aspect.Ratio > 0)
        {
            double ratio = _aspect.Ratio;
            int w = right - left, h = bottom - top;
            if (sx != 0 && sy != 0)                                   // corner: the width decides, the opposite corner stays
            {
                h = Math.Max(1, (int)Math.Round(w / ratio));
                if (sy < 0) top = bottom - h; else bottom = top + h;
            }
            else if (sx != 0)                                         // left / right edge: the height follows, around the middle
            {
                h = Math.Max(1, (int)Math.Round(w / ratio));
                int mid = (_startRect.Y + _startRect.Bottom) / 2;
                top = mid - h / 2; bottom = top + h;
            }
            else                                                      // top / bottom edge: the width follows
            {
                w = Math.Max(1, (int)Math.Round(h * ratio));
                int mid = (_startRect.X + _startRect.Right) / 2;
                left = mid - w / 2; right = left + w;
            }
            if (left < bounds.X || top < bounds.Y || right > bounds.Right || bottom > bounds.Bottom) return;    // would leave the picture: keep the last good box
        }
        SetCropRect(new PixelRect(left, top, right - left, bottom - top));
    }

    // ------------------------------------------------------------------ apply

    private void Apply()
    {
        if (!HasCropRect) return;
        var rect = _cropRect;
        var layer = Document.ActiveLayer;
        if (layer == null || !layer.IsVisible || layer.IsAdjustment)
        {
            Host.SetStatus("Select a visible layer to crop (the box stays, so you can pick one and press Enter).");
            return;
        }
        ClearCrop();

        // The cropped part becomes a new layer above the original, and the original is hidden (not changed or cut).
        var piece = layer.Duplicate(layer.Name + " (cropped)");
        piece.RestoreState(LayerCrop.ToRectangle(piece, rect));
        int index = Document.Layers.ToList().IndexOf(layer);
        var insert = LayerStructureAction.Record("Crop", Document, () =>
        {
            var layers = Document.Layers.ToList();
            layers.Insert(index + 1, piece);
            Document.SetLayers(layers, piece);
        });
        layer.IsVisible = false;
        Push(new CompositeAction("Crop", new IHistoryAction[] { insert, new LayerPropertyAction(layer, LayerProperty.Visibility, true, false) }));
        Host.SelectOnly(piece);
        Host.SelectTool<MoveTool>();
        Host.SetStatus($"Cropped to {rect.Width} x {rect.Height} on a new layer \"{piece.Name}\". \"{layer.Name}\" is hidden, not changed (show it with the eye icon). The canvas keeps its size. Ctrl+Z undoes it.");
    }

    // ------------------------------------------------------------------ aspect

    /// <summary>Shrinks a new box to the chosen aspect, keeping the corner where the drag started.</summary>
    private PixelRect ConstrainNew(PixelRect rect, int anchorX, int anchorY)
    {
        if (_aspect.Ratio <= 0 || rect.IsEmpty) return rect;
        int w = rect.Width, h = rect.Height;
        if (w / (double)h > _aspect.Ratio) w = Math.Max(1, (int)Math.Round(h * _aspect.Ratio));
        else h = Math.Max(1, (int)Math.Round(w / _aspect.Ratio));
        int x = rect.X == anchorX ? rect.X : rect.Right - w;
        int y = rect.Y == anchorY ? rect.Y : rect.Bottom - h;
        return new PixelRect(x, y, w, h);
    }

    /// <summary>The largest box of the chosen aspect that fits inside <paramref name="rect"/>, centered in it.</summary>
    private PixelRect FitAspect(PixelRect rect)
    {
        if (_aspect.Ratio <= 0 || rect.IsEmpty) return rect;
        int w = rect.Width, h = rect.Height;
        if (w / (double)h > _aspect.Ratio) w = Math.Max(1, (int)Math.Round(h * _aspect.Ratio));
        else h = Math.Max(1, (int)Math.Round(w / _aspect.Ratio));
        return new PixelRect(rect.X + (rect.Width - w) / 2, rect.Y + (rect.Height - h) / 2, w, h);
    }

    // ------------------------------------------------------------------ drawing

    private void SetCropRect(PixelRect rect)
    {
        _cropRect = rect;
        if (_shade == null)
        {
            _shade = new Path { Fill = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), Stroke = Brushes.White, IsHitTestVisible = false };
            Host.Canvas?.AddOverlay(_shade);
            _handles = new Path { Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromRgb(40, 40, 40)), IsHitTestVisible = false };
            Host.Canvas?.AddOverlay(_handles);
        }
        // Darken everything outside the crop box.
        var outside = new GeometryGroup { FillRule = FillRule.EvenOdd };
        outside.Children.Add(new RectangleGeometry(new Rect(0, 0, Document.Width, Document.Height)));
        outside.Children.Add(new RectangleGeometry(new Rect(rect.X, rect.Y, rect.Width, rect.Height)));
        _shade.Data = outside;

        // Eight small squares, a constant size on screen whatever the zoom.
        double size = 9 / Math.Max(0.01, Host.Canvas?.Zoom ?? 1), half = size / 2;
        double l = rect.X, t = rect.Y, r = rect.Right, b = rect.Bottom, cx = (l + r) / 2, cy = (t + b) / 2;
        var squares = new GeometryGroup();
        foreach (var (x, y) in new[] { (l, t), (cx, t), (r, t), (l, cy), (r, cy), (l, b), (cx, b), (r, b) })
            squares.Children.Add(new RectangleGeometry(new Rect(x - half, y - half, size, size)));
        _handles!.Data = squares;

        OnPropertyChanged(nameof(HasCropRect));
        OnPropertyChanged(nameof(SizeText));
    }

    private void ClearCrop()
    {
        if (_shade != null) Host.Canvas?.RemoveOverlay(_shade);
        if (_handles != null) Host.Canvas?.RemoveOverlay(_handles);
        _shade = null;
        _handles = null;
        _target = null;
        _cropRect = PixelRect.Empty;
        _drag = Drag.None;
        OnPropertyChanged(nameof(HasCropRect));
        OnPropertyChanged(nameof(SizeText));
    }
}
