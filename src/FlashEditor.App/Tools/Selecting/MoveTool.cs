using System.Windows.Input;
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
    private LayerObject? _startObject;
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
    public override object Options => "Click to select, Shift+click to select several (Ctrl+G groups them). Drag to move, handles resize / rotate. "
        + "Double-click text to edit. Right-click for more.";

    private double Zoom => Host.Canvas?.Zoom ?? 1;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        _start = e.Position;

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
        if (single && !e.Shift && active is { IsLocked: false } && TransformHandles.FrameOf(active) is { } frame)
        {
            var handle = TransformHandles.HitTest(frame, active!.Object, e.Position, Zoom);
            if (handle is not (HandleKind.None or HandleKind.Move))
            {
                BeginObjectDrag(active, handle, makeObject: true);
                return;
            }
        }

        // 3) Whatever is under the mouse: select it and start moving it.
        var hit = Host.FindLayerAt(e.Position, 3 / Zoom);
        if (hit == null)
        {
            Host.SelectOnly(Document.ActiveLayer);
            Host.SetStatus("Nothing here. Click a shape, text, image or painted area to select it.");
            return;
        }

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
        if (hit.Object != null) BeginObjectDrag(hit, HandleKind.Move, makeObject: false);
        else _pixels.Begin(hit);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
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

        if (_objectLayer != null && _startObject != null)
        {
            _objectLayer.SetObject(Dragged(_startObject, e));
            return;
        }

        UpdateHoverCursor(e.Position);
    }

    public override void MouseUp(ToolMouseArgs e) => Finish();

    public override void Deactivate() => Finish();

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
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

    // ------------------------------------------------------------------ objects

    private void BeginObjectDrag(Layer layer, HandleKind handle, bool makeObject)
    {
        if (layer.Object == null)
        {
            if (!makeObject || !MakeResizable(layer)) return;
        }
        _objectLayer = layer;
        _startObject = layer.Object;
        _handle = handle;
        _actionName = handle switch
        {
            HandleKind.Move => "Move",
            HandleKind.Rotate => "Rotate",
            _ => "Resize",
        };
    }

    /// <summary>Turns a pixel layer into an image object (so it can be resized and rotated cleanly).</summary>
    private bool MakeResizable(Layer layer)
    {
        if (!layer.IsVisible) return false;
        var bounds = layer.VisibleBounds();
        if (bounds.IsEmpty) return false;
        var (content, x, y) = layer.GetFullContent();
        var source = content.Crop(bounds.Offset(-x, -y));
        var before = layer.SaveState();
        layer.SetObject(new ImageObject(new ObjectBox(bounds.X, bounds.Y, bounds.Width, bounds.Height), source, layer.Name));
        Push(new LayerContentAction("Make Resizable", layer, before, layer.SaveState()));
        return true;
    }

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
        _handle = HandleKind.None;
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
        if (active is { IsLocked: false } && TransformHandles.FrameOf(active) is { } frame)
        {
            var handle = TransformHandles.HitTest(frame, active!.Object, position, Zoom);
            if (handle != HandleKind.None) cursor = TransformHandles.CursorFor(handle, frame.Rotation);
        }
        if (cursor == null && Host.FindLayerAt(position, 3 / Zoom) != null) cursor = Cursors.SizeAll;
        Host.Canvas?.SetCursorOverride(cursor);
    }
}
