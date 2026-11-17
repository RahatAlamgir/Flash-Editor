using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Select and move, Canva-style:
/// <list type="bullet">
/// <item>Click anything (shape, text, image, or painted pixels) to select its layer.</item>
/// <item>Drag to move it. Drag the box handles to resize (corners keep the shape of images and text;
/// Shift switches), or the round handle to rotate (Shift = 15° steps).</item>
/// <item>With a pixel selection, dragging inside it moves the selected pixels instead.</item>
/// <item>Double-click text to edit it. Arrow keys nudge (Shift = 10 px).</item>
/// </list>
/// Resizing or rotating a painted (pixel) layer first turns it into an image object, so it can be
/// resized again later without losing quality.
/// </summary>
public sealed class MoveTool : ToolBase
{
    private readonly PixelMover _pixels;
    private readonly MultiMover _multi;

    // Object drag state
    private Layer? _objectLayer;
    private LayerObject? _startObject;    // the layer's object when the drag began (for undo)
    private int? _cornerIndex;            // set while a corner point of a shape is being dragged (reshape)
    private int? _partIndex;              // set when a part inside a group is being dragged...
    private LayerObject? _startPart;      // ...and how that part looked (on the canvas) when the drag began
    private HandleKind _handle;
    private PointD _start;
    private string _actionName = "Move";

    public MoveTool(IToolHost host) : base(host)
    {
        _pixels = new PixelMover(host);
        _multi = new MultiMover(host);
    }

    public override string Name => "Select / Move";
    public override string IconKey => "Move";
    public override Key Shortcut => Key.V;
    public override Cursor Cursor => Cursors.Arrow;
    public override bool ShowsTransformFrame => true;
    public override object Options => "Click to select, Shift+click or Ctrl+drag a box to select several (Ctrl+G groups them). Drag to move, handles resize / rotate. "
        + "Double-click a group to edit a part of it (Esc = back to the group), double-click text to edit it. Right-click for more.";

    private double Zoom => Host.Canvas?.Zoom ?? 1;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        _start = e.Position;

        // Ctrl + drag: rubber band that selects every object it touches (even when started on an object).
        if (e.Ctrl)
        {
            _marqueeStart = e.Position;
            _marqueeAdds = e.Shift;
            return;
        }

        // 1) Inside a pixel selection: move the selected pixels.
        if (!e.Shift && Document.Selection is { } selection && selection.IsSelected(e.X, e.Y) && Document.ActiveLayer != null)
        {
            if (!TryGetEditableLayer(out var editable)) return;
            _pixels.Begin(editable);
            return;
        }

        // 2) A handle of the selected layer's box.
        var active = Document.ActiveLayer;
        bool single = Host.SelectedLayers.Count <= 1;
        int? activePart = Host.ActivePartIndex;
        // While reshaping, the corner points of the shape are the handles; the box handles are hidden and not used.
        if (Host.IsReshaping && Document.ActiveLayer is { Object: ShapeObject reshaped } shapeLayer && TransformHandles.VertexAt(reshaped, e.Position, Zoom) is { } corner)
        {
            BeginCornerDrag(shapeLayer, reshaped, corner);
            return;
        }

        if (single && !e.Shift && !Host.IsReshaping && active is { IsLocked: false } && TransformHandles.FrameOf(active, activePart) is { } frame)
        {
            var handle = TransformHandles.HitTest(frame, new EditTarget(active, activePart).Object, e.Position, Zoom);
            if (handle is not (HandleKind.None or HandleKind.Move))
            {
                BeginObjectDrag(active!, handle, makeObject: true, activePart);
                return;
            }
        }

        // 3) Whatever is under the mouse: select it and start moving it.
        var hit = Host.FindLayerAt(e.Position, 3 / Zoom);
        if (hit == null)
        {
            Host.SelectOnly(Document.ActiveLayer);
            Host.SetStatus(Host.FindLayerAt(e.Position, 3 / Zoom, includeLocked: true) is { } locked
                ? $"\"{locked.Name}\" is locked, so it cannot be picked on the canvas. Select it in the Layers panel to unlock it."
                : "Nothing here. Click a shape, text, image or painted area to select it.");
            return;
        }

        // A locked object above what was hit is ignored: say so, so it is clear why the click went "through" it.
        if (Host.FindLayerAt(e.Position, 3 / Zoom, includeLocked: true) is { IsLocked: true } lockedAbove && !ReferenceEquals(lockedAbove, hit))
            Host.SetStatus($"\"{lockedAbove.Name}\" is locked, so the click went through it. Select it in the Layers panel to unlock it.");

        // Shift+click: add to / remove from the selection.
        if (e.Shift)
        {
            Host.ToggleSelected(hit);
            return;
        }

        // Dragging one of several selected layers moves them all.
        var selectedLayers = Host.SelectedLayers;
        if (selectedLayers.Count > 1 && selectedLayers.Contains(hit))
        {
            Document.ActiveLayer = hit;
            _multi.Begin(selectedLayers);
            return;
        }

        // A group: double-click goes inside it (selects the part under the mouse). Once inside, a click picks
        // the part under the mouse and dragging moves just that part. Esc, or clicking elsewhere, leaves the group.
        if (hit.Object is GroupObject group && (e.ClickCount == 2 || (Host.ActivePartIndex != null && ReferenceEquals(hit, active))))
        {
            int? index = PartAt(group, e.Position);
            if (index != null)
            {
                bool wasSelected = Host.ActivePartIndex == index && ReferenceEquals(hit, active);
                Host.SelectPart(hit, index);
                if (hit.IsLocked)
                {
                    Host.SetStatus($"\"{hit.Name}\" is locked, so its parts cannot be changed. Right-click > Unlock (or Ctrl+L).");
                    return;
                }
                var part = new EditTarget(hit, index).Object;
                if (e.ClickCount == 2 && wasSelected && part is TextObject)
                {
                    EditPartText(hit, index!.Value);
                    return;
                }
                Host.SetStatus($"Editing \"{group.Members[index!.Value].Name}\" inside the group. Esc goes back to the whole group.");
                BeginObjectDrag(hit, HandleKind.Move, makeObject: false, index);
                return;
            }
        }

        Host.SelectOnly(hit);

        if (hit.IsLocked)
        {
            Host.SetStatus($"\"{hit.Name}\" is locked, so it cannot be moved. Right-click > Unlock (or Ctrl+L) to change it.");
            return;
        }
        if (e.ClickCount == 2 && hit.Object is TextObject)
        {
            EditText(hit);
            return;
        }
        if (hit.Object != null) BeginObjectDrag(hit, HandleKind.Move, makeObject: false, part: null);
        else _pixels.Begin(hit);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_marqueeStart != null)
        {
            Host.Canvas?.ShowSelectionPreview(new RectangleGeometry(MarqueeRect(e.Position)));
            return;
        }
        if (_multi.IsMoving)
        {
            int mdx = (int)Math.Round(e.Position.X - _start.X), mdy = (int)Math.Round(e.Position.Y - _start.Y);
            if (e.Shift) { if (Math.Abs(mdx) > Math.Abs(mdy)) mdy = 0; else mdx = 0; }
            _multi.MoveTo(mdx, mdy);
            return;
        }
        if (_pixels.IsMoving)
        {
            int dx = (int)Math.Round(e.Position.X - _start.X);
            int dy = (int)Math.Round(e.Position.Y - _start.Y);
            if (e.Shift) { if (Math.Abs(dx) > Math.Abs(dy)) dy = 0; else dx = 0; }
            _pixels.MoveTo(dx, dy);
            return;
        }

        if (_objectLayer != null && _startObject is ShapeObject cornerShape && _cornerIndex is { } cornerIndex)
        {
            _objectLayer.SetObject(WithMovedCorner(cornerShape, cornerIndex, e.Position, e.Shift));
            return;
        }

        if (_objectLayer != null && _startObject != null)
        {
            // Dragging a part: change the part, then let the group fit itself around its parts again.
            _objectLayer.SetObject(_partIndex is { } part && _startPart != null && _startObject is GroupObject group
                ? group.WithPart(part, Dragged(_startPart, e))
                : Dragged(_startObject, e));
            return;
        }

        UpdateHoverCursor(e.Position);
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (_marqueeStart != null)
        {
            EndMarquee(e.Position);
            return;
        }
        Finish();
    }

    public override void Deactivate()
    {
        CancelMarquee();
        Finish();
    }

    // ------------------------------------------------------------------ rubber-band selection (Ctrl + drag)

    private PointD? _marqueeStart;
    private bool _marqueeAdds;

    private Rect MarqueeRect(PointD current)
    {
        var start = _marqueeStart ?? current;
        return new Rect(new Point(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y)),
                        new Point(Math.Max(start.X, current.X), Math.Max(start.Y, current.Y)));
    }

    private void CancelMarquee()
    {
        if (_marqueeStart == null) return;
        _marqueeStart = null;
        Host.Canvas?.ShowSelectionPreview(null);
    }

    private void EndMarquee(PointD end)
    {
        var rect = MarqueeRect(end);
        bool adds = _marqueeAdds;
        CancelMarquee();

        // A click without dragging selects nothing.
        if (rect.Width < 3 / Zoom && rect.Height < 3 / Zoom) return;

        var found = LayersTouching(rect);
        if (found.Count == 0)
        {
            Host.SetStatus("No object in that area. Locked and hidden layers are skipped; the background layer is never picked this way.");
            return;
        }

        var chosen = adds ? Host.SelectedLayers.Union(found).ToList() : found;
        // Keep the layers in stack order (bottom to top); the top-most one becomes the active layer.
        var ordered = Document.Layers.Where(chosen.Contains).ToList();
        Host.SetSelection(ordered, ordered[^1]);
        Host.SetStatus(ordered.Count == 1
            ? $"Selected \"{ordered[0].Name}\"."
            : $"{ordered.Count} selected. Drag to move them together, Ctrl+G groups them, Ctrl+D duplicates them.");
    }

    /// <summary>
    /// Visible, unlocked layers whose content touches the rectangle. A painted layer counts by the area it has pixels in;
    /// one that covers the whole canvas (a background) is not picked, or every rubber band would select it.
    /// </summary>
    private List<Layer> LayersTouching(Rect rect)
    {
        var result = new List<Layer>();
        foreach (var layer in Document.Layers)
        {
            if (!layer.IsVisible || layer.IsLocked || layer.Opacity <= 0.02) continue;
            if (layer.Object is { } obj)
            {
                if (obj.Box.IntersectsRect(rect.Left, rect.Top, rect.Right, rect.Bottom)) result.Add(layer);
                continue;
            }

            var bounds = layer.VisibleBounds();
            if (bounds.IsEmpty) continue;
            bool coversCanvas = bounds.Width >= Document.Width && bounds.Height >= Document.Height;
            if (coversCanvas) continue;
            if (bounds.X <= rect.Right && bounds.Right >= rect.Left && bounds.Y <= rect.Bottom && bounds.Bottom >= rect.Top)
                result.Add(layer);
        }
        return result;
    }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        // While reshaping a shape, Esc finishes it.
        if (Host.IsReshaping && key == Key.Escape && modifiers == ModifierKeys.None)
        {
            Host.StopReshaping();
            Host.SetStatus("Reshape finished.");
            return true;
        }

        // Inside a group: Esc goes back to the whole group, Delete removes the selected part.
        if (Host.ActivePartIndex != null && modifiers == ModifierKeys.None)
        {
            if (key == Key.Escape)
            {
                Host.SelectPart(Document.ActiveLayer!, null);
                Host.SetStatus("Back to the whole group.");
                return true;
            }
            if (key is Key.Delete or Key.Back) return Host.DeleteActivePart();
        }

        int step = modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        (int dx, int dy) = key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            Key.Down => (0, step),
            _ => (0, 0),
        };
        if (dx == 0 && dy == 0) return false;

        if (Host.SelectedLayers.Count > 1)
        {
            _multi.Begin(Host.SelectedLayers);
            _multi.MoveTo(dx, dy);
            Push(_multi.End());
            Host.Canvas?.RefreshTransformFrame();
            return true;
        }

        if (Document.ActiveLayer is { IsLocked: true } locked)
        {
            Host.SetStatus($"\"{locked.Name}\" is locked.");
            return true;
        }
        if (Document.Selection == null && Document.ActiveLayer is { Object: { } obj } layer)
        {
            if (Host.ActivePartIndex is { } part)
            {
                var target = new EditTarget(layer, part);
                if (target.Object is { } partObject)
                    Push(ObjectChangeAction.Apply("Nudge", layer, target.WithObject(ObjectTransforms.Offset(partObject, dx, dy)), mergeKey: $"Nudge part {part}"));
                return true;
            }
            Push(ObjectChangeAction.Apply("Nudge", layer, ObjectTransforms.Offset(obj, dx, dy), mergeKey: "Nudge"));
            return true;
        }
        if (TryGetEditableLayer(out var editable))
        {
            _pixels.Begin(editable);
            _pixels.MoveTo(dx, dy);
            Push(_pixels.End());
            Host.Canvas?.RefreshTransformFrame();
        }
        return true;
    }


    // ------------------------------------------------------------------ reshape (dragging the corner points of a shape)

    private void BeginCornerDrag(Layer layer, ShapeObject shape, int corner)
    {
        _objectLayer = layer;
        _startObject = shape;
        _partIndex = null;
        _startPart = null;
        _cornerIndex = corner;
        _handle = HandleKind.Move;
        _actionName = "Reshape";
    }

    /// <summary>
    /// The shape with one corner point moved to the mouse. The point stays inside the shape's box. Shift snaps it to a coarse grid.
    /// A shape that still had its normal outline gets custom corner points (and loses rounded corners, which no longer fit).
    /// </summary>
    private static ShapeObject WithMovedCorner(ShapeObject shape, int index, PointD mouse, bool snap)
    {
        var points = (shape.Vertices ?? ShapeVertices.For(shape))!.ToList();
        if (index < 0 || index >= points.Count) return shape;

        var unit = shape.ToUnit(mouse);
        double x = Math.Clamp(unit.X, 0, 1), y = Math.Clamp(unit.Y, 0, 1);
        if (snap) { x = Math.Round(x * 20) / 20; y = Math.Round(y * 20) / 20; }
        points[index] = new PointD(x, y);
        return shape with { Vertices = points, CornerRadius = 0 };
    }

    // ------------------------------------------------------------------ objects

    private void BeginObjectDrag(Layer layer, HandleKind handle, bool makeObject, int? part)
    {
        if (layer.Object == null)
        {
            if (!makeObject || !MakeResizable(layer)) return;
        }
        _objectLayer = layer;
        _startObject = layer.Object;
        _partIndex = part;
        _startPart = part == null ? null : new EditTarget(layer, part).Object;
        if (part != null && _startPart == null) { _objectLayer = null; _startObject = null; _partIndex = null; return; }
        _handle = handle;
        _actionName = handle switch
        {
            HandleKind.Move => "Move",
            HandleKind.Rotate => "Rotate",
            _ => "Resize",
        };
    }

    /// <summary>Turns a pixel layer into an image object (so it can be resized and rotated cleanly).</summary>
    private bool MakeResizable(Layer layer) => Host.MakePictureObject(layer, "Make Resizable");

    private LayerObject Dragged(LayerObject start, ToolMouseArgs e)
    {
        switch (_handle)
        {
            case HandleKind.Move:
            {
                double dx = Math.Round(e.Position.X - _start.X), dy = Math.Round(e.Position.Y - _start.Y);
                if (e.Shift) { if (Math.Abs(dx) > Math.Abs(dy)) dy = 0; else dx = 0; }
                Host.SetStatus($"Move: {dx:+0;-0;0}, {dy:+0;-0;0} px");
                return ObjectTransforms.Offset(start, dx, dy);
            }
            case HandleKind.Rotate:
            {
                double angle = TransformHandles.RotationFor(start.Box, e.Position, e.Shift);
                Host.SetStatus($"Rotation: {angle:0}°");
                return start with { Box = start.Box with { Rotation = angle } };
            }
            default:
            {
                bool keepRatio = start.KeepsAspectByDefault != e.Shift;
                var box = TransformHandles.Resize(start.Box, _handle, e.Position, keepRatio);
                if (start.IsLineLike) box = ObjectBox.FromCenter(box.Center, box.Width, start.Box.Height, box.Rotation);
                Host.SetStatus($"{box.Width:0} x {box.Height:0} px");
                // Text: the font size follows the height so the text stays sharp.
                return start is TextObject text
                    ? text with { Box = box, FontSize = Math.Max(1, text.FontSize * box.Height / start.Box.Height) }
                    : start with { Box = box };
            }
        }
    }

    private void Finish()
    {
        if (_multi.IsMoving)
        {
            Push(_multi.End());
            Host.Canvas?.RefreshTransformFrame();
        }
        if (_pixels.IsMoving)
        {
            Push(_pixels.End());
            Host.Canvas?.RefreshTransformFrame();
        }

        if (_objectLayer != null && _startObject != null && _objectLayer.Object is { } final && !Equals(final, _startObject))
            Push(new ObjectChangeAction(_actionName, _objectLayer, _startObject, final));
        _objectLayer = null;
        _startObject = null;
        _partIndex = null;
        _startPart = null;
        _cornerIndex = null;
        _handle = HandleKind.None;
    }

    /// <summary>The top-most part of a group under the point (by its box), or null.</summary>
    private int? PartAt(GroupObject group, PointD point)
    {
        for (int i = group.Members.Count - 1; i >= 0; i--)
        {
            var part = group.CurrentMember(i).Object;
            if (part.Box.Contains(point, (part.IsLineLike ? 6 : 2) / Zoom)) return i;
        }
        return null;
    }

    private void EditPartText(Layer layer, int index)
    {
        var target = new EditTarget(layer, index);
        if (target.Object is not TextObject text) return;
        var value = Host.Dialogs.ShowTextInput("Edit Text", "Text (Enter = new line):", text.Text);
        if (string.IsNullOrWhiteSpace(value) || value == text.Text) return;
        var updated = text with { Text = value.Replace("\r\n", "\n") };
        var (w, h) = ObjectRendering.Renderer.MeasureText(updated);
        updated = updated with { Box = updated.Box with { Width = w, Height = h } };
        Push(ObjectChangeAction.Apply("Edit Text", layer, target.WithObject(updated)));
    }

    private void EditText(Layer layer)
    {
        if (layer.Object is not TextObject text) return;
        var value = Host.Dialogs.ShowTextInput("Edit Text", "Text (Enter = new line):", text.Text);
        if (string.IsNullOrWhiteSpace(value) || value == text.Text) return;
        var updated = text with { Text = value.Replace("\r\n", "\n") };
        var (w, h) = ObjectRendering.Renderer.MeasureText(updated);
        Push(ObjectChangeAction.Apply("Edit Text", layer, updated with { Box = updated.Box with { Width = w, Height = h } }));
    }

    private void UpdateHoverCursor(PointD position)
    {
        var active = Document.ActiveLayer;
        Cursor? cursor = null;
        if (Host.IsReshaping && active is { Object: ShapeObject shape } && TransformHandles.VertexAt(shape, position, Zoom) != null)
        {
            Host.Canvas?.SetCursorOverride(Cursors.Hand);
            return;
        }
        int? part = Host.ActivePartIndex;
        if (active is { IsLocked: false } && TransformHandles.FrameOf(active, part) is { } frame)
        {
            var handle = TransformHandles.HitTest(frame, new EditTarget(active, part).Object, position, Zoom);
            if (handle != HandleKind.None) cursor = TransformHandles.CursorFor(handle, frame.Rotation);
        }
        if (cursor == null && Host.FindLayerAt(position, 3 / Zoom) != null) cursor = Cursors.SizeAll;
        Host.Canvas?.SetCursorOverride(cursor);
    }
}
