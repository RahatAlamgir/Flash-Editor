using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using Path = System.Windows.Shapes.Path;

namespace FlashEditor.App.Tools.Shapes;

/// <summary>
/// Curve tool: draw a shape or line made of Bezier curves, and edit it afterwards.
/// <list type="bullet">
/// <item>Click = a corner point. Click and drag = a smooth point (the drag pulls out the curve handles).</item>
/// <item>Click the first point to close the shape (it gets a fill); double-click or Enter ends an open line. Backspace removes the last point, Esc cancels.</item>
/// <item>With a curve selected: drag its points and handles. Double-click a point to make it smooth or sharp, double-click the line to add a point, Delete removes the selected point. Alt while dragging a handle bends one side only.</item>
/// </list>
/// The result is a normal shape: fill, border, dashes, gradient, shadow and all the other settings work on it.
/// </summary>
public sealed class CurveTool : ToolBase
{
    private readonly List<CurveNode> _drawing = new();       // the curve being drawn, in canvas pixels
    private bool _dragging;                                  // pulling the handles of the point that was just added
    private PointD _mouse;
    private Path? _preview, _handles;

    // Editing a curve that already exists.
    private Layer? _editLayer;
    private ShapeObject? _before;
    private int _selected = -1, _grabNode = -1, _grabPart;   // _grabPart: 0 = the point, 1 = handle in, 2 = handle out
    private bool _moved;

    public CurveTool(IToolHost host) : base(host) { }

    public override string Name => "Curve (Bezier)";
    public override string IconKey => "Curve";
    public override Key Shortcut => Key.P;
    public override object Options => "Click = corner, click and drag = smooth curve. Click the first point to close the shape, double-click or Enter to end a line. "
        + "Select a curve to edit it: drag points and handles, double-click a point to make it smooth / sharp, double-click the line to add a point.";

    private bool IsDrawing => _drawing.Count > 0;
    private double Zoom => Math.Max(0.01, Host.Canvas?.Zoom ?? 1);

    public override void Activate()
    {
        Host.SetStatus("Curve: click to place points (drag for a smooth curve). Click the first point to close, Enter to finish.");
        RefreshHandles();
    }

    public override void Deactivate()
    {
        CancelDrawing();
        _editLayer = null;
        RemoveOverlays();
    }

    // ------------------------------------------------------------------ mouse

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;

        if (!IsDrawing && EditTarget(out var layer, out var shape))
        {
            if (TryStartEdit(layer, shape, e)) return;
            // Clicked beside the selected curve: a new curve starts here.
        }

        if (IsDrawing && e.ClickCount >= 2) { Finish(closed: false); return; }
        if (_drawing.Count >= 3 && _drawing[0].Point.DistanceTo(e.Position) <= 9 / Zoom) { Finish(closed: true); return; }

        _drawing.Add(CurveNode.Corner(e.Position));
        _dragging = true;
        _mouse = e.Position;
        RefreshDrawing();
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        _mouse = e.Position;
        if (_grabNode >= 0 && _editLayer != null && _before != null) { Drag(e); return; }
        if (!IsDrawing) return;
        if (_dragging)
        {
            var last = _drawing[^1];
            _drawing[^1] = last.Point.DistanceTo(e.Position) > 3 / Zoom ? CurveNode.Smooth(last.Point, e.Position) : CurveNode.Corner(last.Point);
        }
        RefreshDrawing();
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        _dragging = false;
        if (_grabNode >= 0 && _editLayer != null && _before != null)
        {
            _grabNode = -1;
            if (_moved && _editLayer.Object is ShapeObject after && !ReferenceEquals(after, _before))
                Push(new ObjectChangeAction("Edit Curve", _editLayer, _before, after));
            _moved = false;
            _before = null;
            RefreshHandles();
        }
    }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (IsDrawing)
        {
            switch (key)
            {
                case Key.Enter: Finish(closed: false); return true;
                case Key.Escape: CancelDrawing(); Host.SetStatus("Curve cancelled."); return true;
                case Key.Back:
                    _drawing.RemoveAt(_drawing.Count - 1);
                    if (IsDrawing) RefreshDrawing(); else CancelDrawing();
                    return true;
            }
            return false;
        }
        if (key == Key.Delete && _selected >= 0 && EditTarget(out var layer, out var shape) && shape.Nodes is { } nodes)
        {
            int minimum = shape.Closed ? 3 : 2;
            if (nodes.Count <= minimum) { Host.SetStatus($"A curve needs at least {minimum} points."); return true; }
            var canvas = ToCanvas(shape);
            canvas.RemoveAt(_selected);
            _selected = Math.Min(_selected, canvas.Count - 1);
            Commit(layer, shape, canvas, "Delete Curve Point");
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ drawing

    private void Finish(bool closed)
    {
        int needed = closed ? 3 : 2;
        if (_drawing.Count < needed) { CancelDrawing(); return; }
        var nodes = _drawing.ToList();
        CancelDrawing();

        var (box, unit) = CurvePath.Fit(nodes, closed);
        var color = Host.Colors.Foreground with { A = 255 };
        var shape = new ShapeObject(box, ShapeKind.Path, closed ? color : null, closed ? null : color, closed ? 0 : 4, Nodes: unit, Closed: closed);
        var layer = Host.AddObjectLayer(shape, "Add Curve");
        _editLayer = layer;
        _selected = -1;
        Host.SetStatus(closed ? "Curve added. Drag its points to reshape it; change fill and border in Properties." : "Line added. Drag its points to reshape it; change color and width in Properties.");
        RefreshHandles();
    }

    private void CancelDrawing()
    {
        _drawing.Clear();
        _dragging = false;
        if (_preview != null) Host.Canvas?.RemoveOverlay(_preview);
        _preview = null;
        RefreshHandles();
    }

    private void RefreshDrawing()
    {
        EnsureOverlays();
        var figure = new PathFigure { StartPoint = P(_drawing[0].Point), IsFilled = false };
        for (int i = 1; i < _drawing.Count; i++) figure.Segments.Add(Segment(_drawing[i - 1], _drawing[i]));
        if (!_dragging) figure.Segments.Add(new BezierSegment(P(_drawing[^1].HandleOut), P(_mouse), P(_mouse), true));   // the line to the mouse
        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        _preview!.Data = geometry;
        _handles!.Data = HandleGeometry(_drawing, -1, firstBigger: true);
        Host.SetStatus($"{_drawing.Count} point{(_drawing.Count == 1 ? "" : "s")}. Click the first point to close the shape, double-click or Enter to finish a line.");
    }

    private static PathSegment Segment(CurveNode a, CurveNode b)
        => !a.HasHandleOut && !b.HasHandleIn ? new LineSegment(P(b.Point), true) : new BezierSegment(P(a.HandleOut), P(b.HandleIn), P(b.Point), true);

    // ------------------------------------------------------------------ editing

    private bool EditTarget(out Layer layer, out ShapeObject shape)
    {
        layer = null!; shape = null!;
        if (Document.ActiveLayer is not { Object: ShapeObject { Shape: ShapeKind.Path, Nodes: { Count: >= 2 } } s } l || l.IsLocked || !l.IsVisible) return false;
        layer = l; shape = s;
        return true;
    }

    /// <summary>The curve's points in canvas pixels (the shape may be moved, turned or mirrored).</summary>
    private static List<CurveNode> ToCanvas(ShapeObject shape)
    {
        var matrix = ObjectRenderer.LocalToCanvas(shape);
        double w = shape.Box.Width, h = shape.Box.Height;
        PointD Map(PointD u) { var p = matrix.Transform(new Point(u.X * w, u.Y * h)); return new PointD(p.X, p.Y); }
        return shape.Nodes!.Select(n => n.Mapped(Map)).ToList();
    }

    private bool TryStartEdit(Layer layer, ShapeObject shape, ToolMouseArgs e)
    {
        var nodes = ToCanvas(shape);
        double reach = 8 / Zoom;

        // The handles of the selected point come first (they can lie on top of other points).
        if (_selected >= 0 && _selected < nodes.Count)
        {
            var n = nodes[_selected];
            if (n.HasHandleOut && n.HandleOut.DistanceTo(e.Position) <= reach) return Grab(layer, shape, _selected, 2);
            if (n.HasHandleIn && n.HandleIn.DistanceTo(e.Position) <= reach) return Grab(layer, shape, _selected, 1);
        }
        for (int i = nodes.Count - 1; i >= 0; i--)
            if (nodes[i].Point.DistanceTo(e.Position) <= reach)
            {
                _selected = i;
                if (e.ClickCount >= 2)
                {
                    Commit(layer, shape, CurvePath.ToggleSmooth(nodes, shape.Closed, i), "Smooth Curve Point");
                    return true;
                }
                return Grab(layer, shape, i, 0);
            }

        var near = CurvePath.Nearest(nodes, shape.Closed, e.Position);
        if (e.ClickCount >= 2 && near.Segment >= 0 && near.Distance <= reach)
        {
            _selected = near.Segment + 1;
            Commit(layer, shape, CurvePath.Split(nodes, shape.Closed, near.Segment, near.T), "Add Curve Point");
            return true;
        }
        return false;
    }

    private bool Grab(Layer layer, ShapeObject shape, int node, int part)
    {
        _editLayer = layer;
        _before = shape;
        _grabNode = node;
        _grabPart = part;
        _selected = node;
        _moved = false;
        RefreshHandles();
        return true;
    }

    private void Drag(ToolMouseArgs e)
    {
        var shape = _before!;
        var nodes = ToCanvas(shape);
        var n = nodes[_grabNode];
        if (_grabPart == 0)
        {
            var delta = (e.Position.X - n.Point.X, e.Position.Y - n.Point.Y);
            nodes[_grabNode] = n.Moved(delta.Item1, delta.Item2);
        }
        else
        {
            bool smooth = n.IsSmooth && !e.Alt;                 // Alt bends one side only
            double length = _grabPart == 1 ? n.Point.DistanceTo(n.HandleOut) : n.Point.DistanceTo(n.HandleIn);
            var moved = e.Position;
            if (_grabPart == 1)
            {
                var opposite = smooth ? Opposite(n.Point, moved, length) : n.HandleOut;
                nodes[_grabNode] = new CurveNode(n.Point, moved, opposite);
            }
            else
            {
                var opposite = smooth ? Opposite(n.Point, moved, length) : n.HandleIn;
                nodes[_grabNode] = new CurveNode(n.Point, opposite, moved);
            }
        }
        _moved = true;
        _editLayer!.SetObject(Rebuild(shape, nodes));
        RefreshHandles();
    }

    /// <summary>The place on the other side of the point for a smooth join: opposite to <paramref name="handle"/>, keeping its own length.</summary>
    private static PointD Opposite(PointD point, PointD handle, double length)
    {
        double dx = point.X - handle.X, dy = point.Y - handle.Y, d = Math.Sqrt(dx * dx + dy * dy);
        return d < 1e-9 ? point : new PointD(point.X + dx / d * length, point.Y + dy / d * length);
    }

    /// <summary>The shape with new points (given in canvas pixels): its box is fitted around them again and turning / mirroring is baked in.</summary>
    private static ShapeObject Rebuild(ShapeObject old, IReadOnlyList<CurveNode> canvasNodes)
    {
        var (box, unit) = CurvePath.Fit(canvasNodes, old.Closed);
        return old with { Box = box, Nodes = unit, FlipX = false, FlipY = false, Warp = null };
    }

    private void Commit(Layer layer, ShapeObject shape, IReadOnlyList<CurveNode> canvasNodes, string name)
    {
        var after = Rebuild(shape, canvasNodes);
        layer.SetObject(after);
        Push(new ObjectChangeAction(name, layer, shape, after));
        RefreshHandles();
    }

    // ------------------------------------------------------------------ overlays

    private void EnsureOverlays()
    {
        if (_preview == null)
        {
            _preview = new Path { Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xFF)), Fill = Brushes.Transparent, IsHitTestVisible = false };
            Host.Canvas?.AddOverlay(_preview, 1.5);
        }
        if (_handles == null)
        {
            _handles = new Path { Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xFF)), Fill = Brushes.White, IsHitTestVisible = false };
            Host.Canvas?.AddOverlay(_handles, 1.2);
        }
    }

    private void RemoveOverlays()
    {
        if (_preview != null) Host.Canvas?.RemoveOverlay(_preview);
        if (_handles != null) Host.Canvas?.RemoveOverlay(_handles);
        _preview = _handles = null;
    }

    /// <summary>Shows the points and handles of the selected curve (when nothing is being drawn).</summary>
    public void RefreshHandles()
    {
        if (IsDrawing) return;
        if (_preview != null) { Host.Canvas?.RemoveOverlay(_preview); _preview = null; }
        if (!EditTarget(out _, out var shape)) { RemoveOverlays(); return; }
        EnsureOverlays();
        _handles!.Data = HandleGeometry(ToCanvas(shape), _selected, firstBigger: false);
    }

    private Geometry HandleGeometry(IReadOnlyList<CurveNode> nodes, int selected, bool firstBigger)
    {
        double r = 4 / Zoom;
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            double size = (firstBigger && i == 0 ? r * 1.7 : r) * (i == selected ? 1.3 : 1);
            group.Children.Add(new RectangleGeometry(new Rect(n.Point.X - size, n.Point.Y - size, size * 2, size * 2)));
            if (i == selected || IsDrawing)
            {
                if (n.HasHandleOut) { group.Children.Add(new LineGeometry(P(n.Point), P(n.HandleOut))); group.Children.Add(new EllipseGeometry(P(n.HandleOut), r * 0.8, r * 0.8)); }
                if (n.HasHandleIn) { group.Children.Add(new LineGeometry(P(n.Point), P(n.HandleIn))); group.Children.Add(new EllipseGeometry(P(n.HandleIn), r * 0.8, r * 0.8)); }
            }
        }
        group.Freeze();
        return group;
    }

    private static Point P(PointD p) => new(p.X, p.Y);
}
