using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>Freehand selection: drag around an area; releasing the mouse closes the shape.</summary>
public sealed class LassoTool : ToolBase
{
    private readonly List<PointD> _points = new();
    private bool _drawing;
    private ModifierKeys _startModifiers;

    public LassoTool(IToolHost host) : base(host) { }

    public override string Name => "Lasso";
    public override string IconKey => "Lasso";
    public override Key Shortcut => Key.L;
    public override object Options => "Hold the mouse button and draw around an area. Release to close the selection. "
        + "Shift = add, Alt = subtract. Press L to switch to the Polygon Lasso.";

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        _points.Clear();
        _drawing = true;
        _startModifiers = e.Modifiers;
        AddPoint(e.Position);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (!_drawing) return;
        // Skip points closer than ~1 screen pixel to keep the polygon small.
        double minDistance = 1.0 / (Host.Canvas?.Zoom ?? 1.0);
        if (_points.Count == 0 || _points[^1].DistanceTo(e.Position) >= minDistance)
            AddPoint(e.Position);
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (!_drawing) return;
        _drawing = false;
        Host.Canvas?.ShowSelectionPreview(null);

        var drawn = _points.Count >= 3
            ? Core.Selections.Selection.FromPolygon(_points.ToArray(), Document.Width, Document.Height)
            : null;
        _points.Clear();
        Host.SetStatus(SelectionCombiner.Apply(Document, drawn, _startModifiers));
    }

    public override void Deactivate()
    {
        if (_drawing) Host.Canvas?.ShowSelectionPreview(null);
        _drawing = false;
        _points.Clear();
    }

    private void AddPoint(PointD p)
    {
        _points.Add(p);
        if (_points.Count < 2) return;

        // Closed figure: the line back to the start shows where the selection will end.
        var figure = new PathFigure { StartPoint = new Point(_points[0].X, _points[0].Y), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(_points.Skip(1).Select(q => new Point(q.X, q.Y)), true));
        var geometry = new PathGeometry(new[] { figure }) { FillRule = FillRule.EvenOdd };
        geometry.Freeze();
        Host.Canvas?.ShowSelectionPreview(geometry);
    }
}
