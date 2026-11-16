using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Polygon lasso: click corner after corner (straight lines). Double-click, press Enter, or click the
/// first point to close the shape. Esc cancels, Backspace removes the last point.
/// Shift on the first click = add to the selection, Alt = subtract.
/// </summary>
public sealed class PolygonLassoTool : ToolBase
{
    private readonly List<PointD> _points = new();
    private PointD _mouse;
    private ModifierKeys _startModifiers;

    public PolygonLassoTool(IToolHost host) : base(host) { }

    public override string Name => "Polygon Lasso";
    public override string IconKey => "PolygonLasso";
    public override Key Shortcut => Key.L;
    public override object Options => "Click to add corners. Double-click, Enter, or click the first point to finish. "
        + "Backspace = undo last point, Esc = cancel. Press L to switch lasso types.";

    private bool IsDrawing => _points.Count > 0;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        if (!IsDrawing) _startModifiers = e.Modifiers;

        double closeDistance = 8 / (Host.Canvas?.Zoom ?? 1);
        if (IsDrawing && (e.ClickCount >= 2 || (_points.Count >= 3 && _points[0].DistanceTo(e.Position) <= closeDistance)))
        {
            Finish();
            return;
        }
        _points.Add(e.Position);
        _mouse = e.Position;
        UpdatePreview();
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (!IsDrawing) return;
        _mouse = e.Position;
        UpdatePreview();
    }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (!IsDrawing) return false;
        switch (key)
        {
            case Key.Enter: Finish(); return true;
            case Key.Escape: Cancel(); return true;
            case Key.Back:
                _points.RemoveAt(_points.Count - 1);
                if (IsDrawing) UpdatePreview(); else Cancel();
                return true;
        }
        return false;
    }

    public override void Deactivate() => Cancel();

    private void Finish()
    {
        var points = _points.ToArray();
        Cancel();
        var drawn = points.Length >= 3 ? Selection.FromPolygon(points, Document.Width, Document.Height) : null;
        Host.SetStatus(SelectionCombiner.Apply(Document, drawn, _startModifiers));
    }

    private void Cancel()
    {
        _points.Clear();
        Host.Canvas?.ShowSelectionPreview(null);
    }

    private void UpdatePreview()
    {
        var figure = new PathFigure { StartPoint = new Point(_points[0].X, _points[0].Y), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(_points.Skip(1).Append(_mouse).Select(p => new Point(p.X, p.Y)), true));
        var geometry = new PathGeometry(new[] { figure }) { FillRule = FillRule.EvenOdd };
        geometry.Freeze();
        Host.Canvas?.ShowSelectionPreview(geometry);
        Host.SetStatus($"{_points.Count} point(s). Double-click or Enter to finish.");
    }
}
