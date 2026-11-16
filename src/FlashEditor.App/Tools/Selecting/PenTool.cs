using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Pen tool: draw a smooth outline with curves, then turn it into a selection.
/// <list type="bullet">
/// <item>Click = corner point. Click and drag = smooth point (drag sets the curve's direction and bend).</item>
/// <item>Click the first point, double-click, or press Enter to close the shape and select it.</item>
/// <item>Backspace removes the last point, Esc cancels. Shift on the first click = add to the selection, Alt = subtract.</item>
/// </list>
/// </summary>
public sealed class PenTool : ToolBase
{
    /// <summary>One point of the path. <see cref="Out"/> is the curve handle (null = sharp corner).</summary>
    private sealed class Anchor
    {
        public PointD Point;
        public PointD? Out;
        /// <summary>Handle on the incoming side: the mirror of <see cref="Out"/> (smooth point).</summary>
        public PointD In => Out is { } o ? new PointD(2 * Point.X - o.X, 2 * Point.Y - o.Y) : Point;
        public PointD OutOrPoint => Out ?? Point;
    }

    private readonly List<Anchor> _anchors = new();
    private bool _dragging;
    private PointD _mouse;
    private ModifierKeys _startModifiers;
    private Path? _handles;

    public PenTool(IToolHost host) : base(host) { }

    public override string Name => "Pen (curve selection)";
    public override string IconKey => "Pen";
    public override Key Shortcut => Key.P;
    public override object Options => "Click = corner, click and drag = smooth curve. Click the first point, double-click or press Enter to select the shape. "
        + "Backspace = undo last point, Esc = cancel. Shift = add, Alt = subtract.";

    private bool IsDrawing => _anchors.Count > 0;
    private double Zoom => Host.Canvas?.Zoom ?? 1;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        if (!IsDrawing) _startModifiers = e.Modifiers;

        if (IsDrawing && e.ClickCount >= 2)
        {
            Finish();
            return;
        }
        if (_anchors.Count >= 2 && _anchors[0].Point.DistanceTo(e.Position) <= 8 / Zoom)
        {
            Finish();
            return;
        }

        _anchors.Add(new Anchor { Point = e.Position });
        _dragging = true;
        _mouse = e.Position;
        UpdatePreview();
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (!IsDrawing) return;
        _mouse = e.Position;
        if (_dragging)
        {
            // Dragging after a click pulls out the curve handle (makes a smooth point).
            var last = _anchors[^1];
            last.Out = last.Point.DistanceTo(e.Position) > 2 / Zoom ? e.Position : null;
        }
        UpdatePreview();
    }

    public override void MouseUp(ToolMouseArgs e) => _dragging = false;

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (!IsDrawing) return false;
        switch (key)
        {
            case Key.Enter: Finish(); return true;
            case Key.Escape: Cancel(); Host.SetStatus("Pen path cancelled."); return true;
            case Key.Back:
                _anchors.RemoveAt(_anchors.Count - 1);
                if (IsDrawing) UpdatePreview(); else Cancel();
                return true;
        }
        return false;
    }

    public override void Deactivate() => Cancel();

    /// <summary>Closes the path and turns it into a selection.</summary>
    private void Finish()
    {
        if (_anchors.Count < 2)
        {
            Cancel();
            return;
        }
        var geometry = BuildGeometry(closed: true, previewToMouse: false);
        var flat = geometry.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
        var points = new List<PointD>();
        foreach (var figure in flat.Figures)
        {
            points.Add(new PointD(figure.StartPoint.X, figure.StartPoint.Y));
            foreach (var segment in figure.Segments)
            {
                if (segment is PolyLineSegment poly) points.AddRange(poly.Points.Select(p => new PointD(p.X, p.Y)));
                else if (segment is LineSegment line) points.Add(new PointD(line.Point.X, line.Point.Y));
            }
        }
        Cancel();
        var drawn = points.Count >= 3 ? Selection.FromPolygon(points, Document.Width, Document.Height) : null;
        Host.SetStatus(SelectionCombiner.Apply(Document, drawn, _startModifiers));
    }

    private void Cancel()
    {
        _anchors.Clear();
        _dragging = false;
        Host.Canvas?.ShowSelectionPreview(null);
        if (_handles != null) Host.Canvas?.RemoveOverlay(_handles);
        _handles = null;
    }

    /// <summary>The path through all points (cubic curves between them).</summary>
    private PathGeometry BuildGeometry(bool closed, bool previewToMouse)
    {
        var figure = new PathFigure { StartPoint = P(_anchors[0].Point), IsClosed = closed, IsFilled = true };
        for (int i = 1; i < _anchors.Count; i++) figure.Segments.Add(Curve(_anchors[i - 1], _anchors[i]));
        if (previewToMouse && !_dragging)
            figure.Segments.Add(new BezierSegment(P(_anchors[^1].OutOrPoint), P(_mouse), P(_mouse), true));
        if (closed && _anchors.Count > 2) figure.Segments.Add(Curve(_anchors[^1], _anchors[0]));
        return new PathGeometry(new[] { figure }) { FillRule = FillRule.EvenOdd };
    }

    private static BezierSegment Curve(Anchor from, Anchor to) => new(P(from.OutOrPoint), P(to.In), P(to.Point), true);

    private static Point P(PointD p) => new(p.X, p.Y);

    private void UpdatePreview()
    {
        var path = BuildGeometry(closed: false, previewToMouse: true);
        path.Freeze();
        Host.Canvas?.ShowSelectionPreview(path);

        // Points (small squares; the first one bigger so it is easy to click to close) and handle lines.
        if (_handles == null)
        {
            _handles = new Path { Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xFF)), Fill = Brushes.White };
            Host.Canvas?.AddOverlay(_handles, screenStrokeThickness: 1.2);
        }
        double r = 3.5 / Zoom;
        var group = new GeometryGroup();
        for (int i = 0; i < _anchors.Count; i++)
        {
            var a = _anchors[i];
            double size = i == 0 ? r * 1.6 : r;
            group.Children.Add(new RectangleGeometry(new Rect(a.Point.X - size, a.Point.Y - size, size * 2, size * 2)));
            if (a.Out is { } o)
            {
                group.Children.Add(new LineGeometry(P(a.In), P(o)));
                group.Children.Add(new EllipseGeometry(P(o), r * 0.8, r * 0.8));
                group.Children.Add(new EllipseGeometry(P(a.In), r * 0.8, r * 0.8));
            }
        }
        group.Freeze();
        _handles.Data = group;
        Host.SetStatus($"{_anchors.Count} point(s). Click the first point (or Enter) to finish.");
    }
}
