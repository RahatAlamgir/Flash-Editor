using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.Tools.Selecting;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Views.Editor;

/// <summary>
/// The box with handles around the selected layer (shown while the Move tool is active).
/// Handles keep the same size on screen at every zoom level.
/// </summary>
public partial class CanvasView
{
    private Layer? _framedLayer;
    private ObjectBox? _frameBox;
    private LayerObject? _frameObject; // what the frame surrounds (the selected part of a group, or the layer's object)
    private Cursor? _cursorOverride;

    /// <summary>Recomputes the box (call after the selected layer's content changed).</summary>
    public void RefreshTransformFrame()
    {
        bool show = ActiveTool?.ShowsTransformFrame == true && _panLast == null && _main?.SelectionCleared != true;
        var layer = _document?.Model.ActiveLayer;
        if (!ReferenceEquals(layer, _framedLayer))
        {
            if (_framedLayer != null)
            {
                _framedLayer.PixelsReplaced -= OnFramedLayerChanged;
                _framedLayer.PropertyChanged -= OnFramedLayerPropertyChanged;
            }
            _framedLayer = layer;
            if (_framedLayer != null)
            {
                _framedLayer.PixelsReplaced += OnFramedLayerChanged;
                _framedLayer.PropertyChanged += OnFramedLayerPropertyChanged;
            }
        }
        // Inside a group the frame goes around the selected part instead of the whole group.
        int? part = _main?.ActivePartIndex;
        _frameBox = show ? TransformHandles.FrameOf(layer, part) : null;
        _frameObject = show ? new EditTarget(layer, part).Object : null;
        _multiBoxes = show && _main != null && _main.SelectedLayers.Count > 1
            ? _main.SelectedLayers.Select(l => TransformHandles.FrameOf(l)).OfType<ObjectBox>().ToList()
            : null;
        DrawTransformFrame();
    }

    private List<ObjectBox>? _multiBoxes;

    /// <summary>Several layers selected: a thin outline around each, and no handles.</summary>
    private void DrawMultiFrame(List<ObjectBox> boxes)
    {
        var outline = new StreamGeometry();
        using (var ctx = outline.Open())
        {
            foreach (var b in boxes)
            {
                var c = b.Corners();
                ctx.BeginFigure(new Point(c[0].X, c[0].Y), false, true);
                ctx.PolyLineTo(c.Skip(1).Select(p => new Point(p.X, p.Y)).ToList(), true, false);
            }
        }
        outline.Freeze();
        FrameOutline.Data = outline;
        FrameHandles.Data = null;
    }

    /// <summary>Cursor shown instead of the tool's own cursor (e.g. over a resize handle). Null = tool cursor.</summary>
    public void SetCursorOverride(Cursor? cursor)
    {
        if (_cursorOverride == cursor) return;
        _cursorOverride = cursor;
        UpdateCursor();
    }

    private void OnFramedLayerChanged(object? sender, EventArgs e) => RefreshTransformFrame();

    private void OnFramedLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Layer.IsVisible) or nameof(Layer.Object) or nameof(Layer.IsLocked)) RefreshTransformFrame();
    }

    /// <summary>Reshape mode: the outline through the shape's corner points, with a round handle on each point (same size on screen at every zoom).</summary>
    private void DrawCornerPoints(LayerObject shape, IReadOnlyList<PointD> corners)
    {
        var canvasPoints = corners.Select(shape.FromUnit).ToList();

        var outline = new StreamGeometry();
        using (var ctx = outline.Open())
        {
            ctx.BeginFigure(new Point(canvasPoints[0].X, canvasPoints[0].Y), false, true);
            ctx.PolyLineTo(canvasPoints.Skip(1).Select(p => new Point(p.X, p.Y)).ToList(), true, false);
        }
        outline.Freeze();
        FrameOutline.Data = outline;

        double radius = TransformHandles.HandleSize / 2 * 1.1 / _zoom;
        var handles = new GeometryGroup();
        foreach (var p in canvasPoints) handles.Children.Add(new EllipseGeometry(new Point(p.X, p.Y), radius, radius));
        handles.Freeze();
        FrameHandles.Data = handles;
    }

    /// <summary>Draws the outline, the resize squares and the round rotate handle for the current zoom.</summary>
    private void DrawTransformFrame()
    {
        if (_multiBoxes is { Count: > 1 } multi)
        {
            DrawMultiFrame(multi);
            return;
        }
        if (_frameBox is not { } box)
        {
            FrameOutline.Data = FrameHandles.Data = null;
            return;
        }

        // Reshape: the shape's own outline and a round point at each corner (instead of the box with square handles).
        if (_main?.IsReshaping == true && _frameObject is { } reshaping && reshaping.ReshapePoints is { Count: >= 3 } points)
        {
            DrawCornerPoints(reshaping, points);
            return;
        }

        var corners = box.Corners();
        var outline = new StreamGeometry();
        using (var ctx = outline.Open())
        {
            ctx.BeginFigure(new Point(corners[0].X, corners[0].Y), false, true);
            ctx.PolyLineTo(corners.Skip(1).Select(c => new Point(c.X, c.Y)).ToList(), true, false);

            // Stem from the top edge to the rotate handle.
            var top = box.ToCanvas(0, -box.Height / 2);
            var rotate = TransformHandles.Position(box, HandleKind.Rotate, _zoom);
            ctx.BeginFigure(new Point(top.X, top.Y), false, false);
            ctx.LineTo(new Point(rotate.X, rotate.Y), true, false);
        }
        outline.Freeze();
        FrameOutline.Data = outline;

        double half = TransformHandles.HandleSize / 2 / _zoom;
        var handles = new GeometryGroup();
        if (_framedLayer?.IsLocked == true)
        {
            // Locked: a small padlock-style dot at the top instead of handles.
            var top = box.ToCanvas(0, -box.Height / 2);
            handles.Children.Add(new RectangleGeometry(new Rect(top.X - half, top.Y - half * 2.6, half * 2, half * 1.6)));
            handles.Freeze();
            FrameHandles.Data = handles;
            return;
        }
        foreach (var kind in TransformHandles.HandlesFor(_frameObject))
        {
            var p = TransformHandles.Position(box, kind, _zoom);
            var square = new RectangleGeometry(new Rect(p.X - half, p.Y - half, half * 2, half * 2))
            {
                Transform = new RotateTransform(box.Rotation, p.X, p.Y),
            };
            handles.Children.Add(square);
        }
        var r = TransformHandles.Position(box, HandleKind.Rotate, _zoom);
        handles.Children.Add(new EllipseGeometry(new Point(r.X, r.Y), half * 1.1, half * 1.1));
        handles.Freeze();
        FrameHandles.Data = handles;
    }
}
