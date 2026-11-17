using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Drag to select a rectangle, or an ellipse/circle (the Ellipse Select variant). Shift while dragging
/// = square / circle; Shift when starting = add to the selection, Alt = subtract. A plain click deselects.
/// </summary>
public sealed class RectSelectTool : ToolBase
{
    private readonly bool _ellipse;
    private bool _dragging;
    private PointD _start;
    private ModifierKeys _startModifiers;

    private readonly SnapDrag _snap;

    public RectSelectTool(IToolHost host, bool ellipse = false) : base(host)
    {
        _ellipse = ellipse;
        _snap = new SnapDrag(host);
    }

    public override string Name => _ellipse ? "Ellipse Select" : "Rectangle Select";
    public override string IconKey => _ellipse ? "EllipseSelect" : "RectSelect";
    public override Key Shortcut => Key.M;
    public override object Options => (_ellipse ? "Drag to select an ellipse (Shift after starting = circle)." : "Drag to select a rectangle (Shift after starting = square).")
        + " Hold Shift before dragging to add, Alt to subtract. Click to deselect. Press M to switch rectangle / ellipse.";

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        e = e with { Position = _snap.Point(e.Position, Host.Canvas?.Zoom ?? 1, !e.Alt) };   // snap to objects, canvas, grid, guides
        _start = e.Position;
        _startModifiers = e.Modifiers;
        _dragging = true;
        UpdatePreview(e);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_dragging) UpdatePreview(e with { Position = _snap.Point(e.Position, Host.Canvas?.Zoom ?? 1, !e.Alt) });
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        e = e with { Position = _snap.Point(e.Position, Host.Canvas?.Zoom ?? 1, !e.Alt) };
        _snap.End();
        Host.Canvas?.ShowSelectionPreview(null);

        var rect = GetRect(e);
        if (rect.Width < 1 || rect.Height < 1)
        {
            if (_startModifiers == ModifierKeys.None) Document.Selection = null; // plain click = deselect
            Host.SetStatus(Document.Selection == null ? "Selection cleared" : "");
            return;
        }
        var drawn = _ellipse
            ? Selection.FromEllipse(rect, Document.Width, Document.Height)
            : Selection.FromRectangle(rect, Document.Width, Document.Height);
        Host.SetStatus(SelectionCombiner.Apply(Document, drawn, _startModifiers));
    }

    public override void Deactivate()
    {
        if (_dragging) Host.Canvas?.ShowSelectionPreview(null);
        _dragging = false;
    }

    private void UpdatePreview(ToolMouseArgs e)
    {
        if (!_dragging) return;
        var rect = GetRect(e);
        var r = new Rect(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));
        Geometry geometry = _ellipse ? new EllipseGeometry(r) : new RectangleGeometry(r);
        geometry.Freeze();
        Host.Canvas?.ShowSelectionPreview(geometry);
        Host.SetStatus($"{rect.Width} x {rect.Height}");
    }

    private PixelRect GetRect(ToolMouseArgs e)
    {
        double ex = e.Position.X, ey = e.Position.Y;
        // Shift pressed after starting the drag = square / circle (Shift at the start means "add").
        bool constrain = e.Shift && !_startModifiers.HasFlag(ModifierKeys.Shift);
        if (constrain)
        {
            double size = Math.Max(Math.Abs(ex - _start.X), Math.Abs(ey - _start.Y));
            ex = _start.X + Math.Sign(ex - _start.X) * size;
            ey = _start.Y + Math.Sign(ey - _start.Y) * size;
        }
        var rect = PixelRect.FromCorners((int)Math.Round(_start.X), (int)Math.Round(_start.Y), (int)Math.Round(ex), (int)Math.Round(ey));
        return _ellipse ? rect : rect.Intersect(Document.Bounds);
    }
}
