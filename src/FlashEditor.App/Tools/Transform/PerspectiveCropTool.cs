using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.App.Tools.Selecting;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.App.Tools.Transform;

/// <summary>
/// Perspective Crop: drag a box over something that was photographed at an angle (a page, a screen, a sign), then drag its four corners onto the
/// corners of the thing (drag inside to move the whole box). Enter cuts it out of the selected layer, straightened into a flat rectangle, as a
/// new layer; the original layer is hidden, not changed, and the canvas keeps its size. Esc starts over.
/// </summary>
public sealed class PerspectiveCropTool : ToolBase
{
    private readonly List<PointD> _points = new();
    private Polygon? _outline;
    private readonly List<Ellipse> _handles = new();
    private int _dragging = -1;
    private bool _drawing, _moving;
    private PointD _start;
    private List<PointD> _startPoints = new();

    public PerspectiveCropTool(IToolHost host) : base(host)
    {
        ApplyCommand = new RelayCommand(Apply, () => _points.Count == 4);
        CancelCommand = new RelayCommand(Clear, () => _points.Count > 0);
    }

    public override string Name => "Perspective Crop";
    public override string IconKey => "PerspectiveCrop";
    public override Key Shortcut => Key.C;
    public override object Options => this;

    public RelayCommand ApplyCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>What to do next (shown in the options bar).</summary>
    public string Hint => _points.Count == 4
        ? $"{SuggestedText} Drag the corners onto the corners of the area, then press Enter."
        : "Drag a box over the area you want to straighten.";

    private string SuggestedText
    {
        get
        {
            var (w, h) = PerspectiveTools.SuggestedSize(_points);
            return $"The result will be {w} x {h} px.";
        }
    }

    public override void Activate()
    {
        Clear();
        Host.SetStatus("Perspective Crop: drag a box over the area, drag its corners onto the real corners, then press Enter.");
    }

    public override void Deactivate() => Clear();

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        double reach = 12 / Math.Max(0.05, Host.Canvas?.Zoom ?? 1);
        _start = e.Position;
        for (int i = 0; i < _points.Count; i++)
            if (Math.Abs(_points[i].X - e.Position.X) < reach && Math.Abs(_points[i].Y - e.Position.Y) < reach) { _dragging = i; return; }

        if (_points.Count == 4 && Inside(e.Position))
        {
            _moving = true;
            _startPoints = _points.ToList();
            return;
        }
        Clear();                                  // outside: draw a new box
        _drawing = true;
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        var bounds = Document.Bounds;
        double cx = Math.Clamp(e.Position.X, 0, bounds.Width), cy = Math.Clamp(e.Position.Y, 0, bounds.Height);
        if (_drawing)
        {
            double x1 = Math.Clamp(_start.X, 0, bounds.Width), y1 = Math.Clamp(_start.Y, 0, bounds.Height);
            double l = Math.Min(x1, cx), r = Math.Max(x1, cx), t = Math.Min(y1, cy), b = Math.Max(y1, cy);
            _points.Clear();
            _points.AddRange(new[] { new PointD(l, t), new PointD(r, t), new PointD(r, b), new PointD(l, b) });
            Redraw();
        }
        else if (_moving)
        {
            double dx = e.Position.X - _start.X, dy = e.Position.Y - _start.Y;
            for (int i = 0; i < 4; i++) _points[i] = new PointD(_startPoints[i].X + dx, _startPoints[i].Y + dy);
            Redraw();
        }
        else if (_dragging >= 0 && _dragging < _points.Count)
        {
            _points[_dragging] = new PointD(cx, cy);
            Redraw();
        }
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (_drawing && _points.Count == 4 && (_points[2].X - _points[0].X < 4 || _points[2].Y - _points[0].Y < 4)) Clear();   // a plain click draws nothing
        _dragging = -1;
        _drawing = _moving = false;
        OnPropertyChanged(nameof(Hint));
        RelayCommand.Refresh();
    }

    private bool Inside(PointD p)
    {
        bool inside = false;
        for (int i = 0, j = _points.Count - 1; i < _points.Count; j = i++)
            if ((_points[i].Y > p.Y) != (_points[j].Y > p.Y) &&
                p.X < (_points[j].X - _points[i].X) * (p.Y - _points[i].Y) / (_points[j].Y - _points[i].Y) + _points[i].X)
                inside = !inside;
        return inside;
    }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Enter && _points.Count == 4) { Apply(); return true; }
        if (key == Key.Escape && _points.Count > 0) { Clear(); return true; }
        return false;
    }

    private void Redraw()
    {
        RemoveOverlays();
        var canvas = Host.Canvas;
        if (canvas != null && _points.Count > 0)
        {
            var brush = new SolidColorBrush(Color.FromRgb(0x4f, 0xc3, 0xff));
            var shape = new Polygon { Stroke = brush, StrokeThickness = 1.5, Fill = new SolidColorBrush(Color.FromArgb(0x30, 0x4f, 0xc3, 0xff)), IsHitTestVisible = false };
            foreach (var p in _points) shape.Points.Add(new System.Windows.Point(p.X, p.Y));
            _outline = shape;
            canvas.AddOverlay(shape, 1.5);
            double radius = 6 / Math.Max(0.05, canvas.Zoom);
            foreach (var p in _points)
            {
                var handle = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = Brushes.White, Stroke = brush, StrokeThickness = 1.5, IsHitTestVisible = false };
                System.Windows.Controls.Canvas.SetLeft(handle, p.X - radius);
                System.Windows.Controls.Canvas.SetTop(handle, p.Y - radius);
                _handles.Add(handle);
                canvas.AddOverlay(handle, 0);
            }
        }
        OnPropertyChanged(nameof(Hint));
        RelayCommand.Refresh();
    }

    private void RemoveOverlays()
    {
        var canvas = Host.Canvas;
        if (_outline != null) canvas?.RemoveOverlay(_outline);
        foreach (var h in _handles) canvas?.RemoveOverlay(h);
        _outline = null;
        _handles.Clear();
    }

    private void Clear()
    {
        _points.Clear();
        _dragging = -1;
        _drawing = _moving = false;
        RemoveOverlays();
        OnPropertyChanged(nameof(Hint));
        RelayCommand.Refresh();
    }

    private void Apply()
    {
        if (_points.Count != 4) return;
        var layer = Document.ActiveLayer;
        if (layer == null || !layer.IsVisible || layer.IsAdjustment)
        {
            Host.SetStatus("Select a visible layer first (the box stays, so you can pick one and press Enter).");
            return;
        }
        var quad = _points.ToList();
        var (w, h) = PerspectiveTools.SuggestedSize(quad);
        w = Math.Clamp(w, 1, 12000);
        h = Math.Clamp(h, 1, 12000);
        var flat = PerspectiveTools.PerspectiveCrop(layer.MaskedPixels(), quad, w, h);
        if (flat == null)
        {
            Host.SetStatus("Those four points do not make a four-sided shape. Move the corners and try again.");
            return;
        }
        Clear();

        // The straightened part becomes a new picture layer (shrunk to fit the canvas, centered on the area it came from);
        // the original layer is hidden, not changed, and the canvas keeps its size.
        double scale = Math.Min(1, Math.Min(Document.Width / (double)w, Document.Height / (double)h));
        double bw = w * scale, bh = h * scale;
        double cx = quad.Average(p => p.X), cy = quad.Average(p => p.Y);
        double bx = Math.Clamp(cx - bw / 2, 0, Math.Max(0, Document.Width - bw)), by = Math.Clamp(cy - bh / 2, 0, Math.Max(0, Document.Height - bh));
        var piece = new Layer(layer.Name + " (straightened)", Document.Width, Document.Height);
        piece.SetObject(new ImageObject(new ObjectBox(bx, by, bw, bh), flat, layer.Name + " (straightened)"));

        int index = Document.Layers.ToList().IndexOf(layer);
        var insert = LayerStructureAction.Record("Perspective Crop", Document, () =>
        {
            var layers = Document.Layers.ToList();
            layers.Insert(index + 1, piece);
            Document.SetLayers(layers, piece);
        });
        layer.IsVisible = false;
        Push(new CompositeAction("Perspective Crop", new IHistoryAction[] { insert, new LayerPropertyAction(layer, LayerProperty.Visibility, true, false) }));
        Host.SelectOnly(piece);
        Host.SelectTool<MoveTool>();
        Host.SetStatus($"Straightened to {w} x {h} px on a new layer. \"{layer.Name}\" is hidden, not changed. The canvas keeps its size. Ctrl+Z undoes it.");
    }
}
