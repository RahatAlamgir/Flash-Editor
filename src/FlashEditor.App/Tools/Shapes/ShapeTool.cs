using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlashEditor.App.Rendering;
using FlashEditor.App.Tools.Options;
using FlashEditor.App.Tools.Selecting;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Tools.Shapes;

/// <summary>
/// Draws a shape (rectangle, circle, star, arrow, line...) as an editable object on its own layer.
/// Drag to size it, or just click for a default size. Shift = equal width and height / 45° line.
/// Afterwards the Move tool is selected so the shape can be adjusted right away.
/// </summary>
public sealed class ShapeTool : ToolBase
{
    private const double DefaultSize = 200;
    private readonly ShapeOptions _options;
    private Path? _preview;
    private PointD _start;
    private readonly SnapDrag _snap;

    public ShapeTool(IToolHost host, ShapeOptions options) : base(host)
    {
        _options = options;
        _snap = new SnapDrag(host);
    }

    public override string Name => "Shapes";
    public override string IconKey => "Shapes";
    public override Key Shortcut => Key.U;
    public override object Options => _options;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        e = e with { Position = _snap.Point(e.Position, Host.Canvas?.Zoom ?? 1, !e.Alt) };   // snap to objects, canvas, grid, guides
        _start = e.Position;
        _preview = new Path { Opacity = 0.8, IsHitTestVisible = false };
        Host.Canvas?.AddOverlay(_preview, screenStrokeThickness: 0);
        UpdatePreview(e);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_preview == null) return;
        UpdatePreview(e with { Position = _snap.Point(e.Position, Host.Canvas?.Zoom ?? 1, !e.Alt) });
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (_preview == null) return;
        RemovePreview();
        e = e with { Position = _snap.Point(e.Position, Host.Canvas?.Zoom ?? 1, !e.Alt) };
        _snap.End();
        var shape = _options.Create(BoxFor(e));
        Host.AddObjectLayer(shape, $"Add {shape.SuggestedLayerName}");
        Host.SelectTool<MoveTool>();
        Host.SetStatus("Shape added. Drag the handles to resize or rotate; change colors in the Properties panel.");
    }

    public override void Deactivate()
    {
        RemovePreview();
        _snap.End();
    }

    /// <summary>The object box for a drag from the start point to the mouse (or a default size for a click).</summary>
    private ObjectBox BoxFor(ToolMouseArgs e)
    {
        var a = _start;
        var b = e.Position;
        bool click = a.DistanceTo(b) < 4 / (Host.Canvas?.Zoom ?? 1);

        if (_options.Kind == ShapeKind.Line)
        {
            if (click) b = new PointD(a.X + DefaultSize, a.Y);
            double angle = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;
            if (e.Shift) angle = Math.Round(angle / 45) * 45;
            double length = Math.Max(1, a.DistanceTo(b));
            double rad = angle * Math.PI / 180;
            var center = new PointD(a.X + Math.Cos(rad) * length / 2, a.Y + Math.Sin(rad) * length / 2);
            return ObjectBox.FromCenter(center, length, Math.Max(1, _options.StrokeWidth), angle);
        }

        if (click) return ObjectBox.FromCenter(a, DefaultSize, DefaultSize);

        double w = b.X - a.X, h = b.Y - a.Y;
        if (e.Shift)
        {
            double size = Math.Max(Math.Abs(w), Math.Abs(h));
            w = Math.Sign(w == 0 ? 1 : w) * size;
            h = Math.Sign(h == 0 ? 1 : h) * size;
        }
        double x = Math.Min(a.X, a.X + w), y = Math.Min(a.Y, a.Y + h);
        return new ObjectBox(Math.Round(x), Math.Round(y), Math.Max(1, Math.Round(Math.Abs(w))), Math.Max(1, Math.Round(Math.Abs(h))));
    }

    private void UpdatePreview(ToolMouseArgs e)
    {
        if (_preview == null) return;
        var shape = _options.Create(BoxFor(e));
        var geometry = ShapeGeometryFactory.Build(shape, shape.Box.Width, shape.Box.Height).Clone();
        geometry.Transform = new MatrixTransform(ObjectRenderer.LocalToCanvas(shape));
        _preview.Data = geometry;
        _preview.Fill = shape.Fill is { } f ? new SolidColorBrush(BitmapConverter.ToWpf(f)) : null;
        _preview.Stroke = shape.Stroke is { } s ? new SolidColorBrush(BitmapConverter.ToWpf(s)) : null;
        _preview.StrokeThickness = shape.Stroke != null ? shape.StrokeWidth : 0;
        _preview.StrokeStartLineCap = _preview.StrokeEndLineCap = shape.IsLineLike ? PenLineCap.Round : PenLineCap.Flat;
    }

    private void RemovePreview()
    {
        if (_preview != null) Host.Canvas?.RemoveOverlay(_preview);
        _preview = null;
    }
}
