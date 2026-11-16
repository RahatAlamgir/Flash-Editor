using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Moves several selected layers together (objects and painted layers alike). Locked layers stay put.
/// The whole move becomes one undo step.
/// </summary>
internal sealed class MultiMover
{
    private sealed record Item(Layer Layer, LayerState Before, LayerObject? StartObject, PixelBuffer? Content, int X, int Y);

    private readonly IToolHost _host;
    private readonly List<Item> _items = new();
    private int _dx, _dy;

    public MultiMover(IToolHost host) => _host = host;

    public bool IsMoving => _items.Count > 0;

    public void Begin(IEnumerable<Layer> layers)
    {
        _items.Clear();
        _dx = _dy = 0;
        foreach (var layer in layers)
        {
            if (layer.IsLocked || !layer.IsVisible) continue;
            if (layer.Object is { } obj)
            {
                _items.Add(new Item(layer, layer.SaveState(), obj, null, 0, 0));
            }
            else
            {
                var (content, x, y) = layer.GetFullContent();
                _items.Add(new Item(layer, layer.SaveState().DeepCopy(), null, content.Clone(), x, y));
            }
        }
    }

    public void MoveTo(int dx, int dy)
    {
        if (dx == _dx && dy == _dy) return;
        _dx = dx;
        _dy = dy;
        foreach (var item in _items)
        {
            if (item.StartObject != null)
            {
                item.Layer.SetObject(ObjectTransforms.Offset(item.StartObject, dx, dy));
            }
            else
            {
                item.Layer.Pixels.Clear();
                item.Layer.Pixels.CopyFrom(item.Content!, item.X + dx, item.Y + dy);
                item.Layer.NotifyPixelsChanged();
            }
        }
        _host.SetStatus($"Move {_items.Count} layers: {dx:+0;-0;0}, {dy:+0;-0;0} px");
    }

    /// <summary>Finishes and returns one undo step for all layers (null when nothing moved).</summary>
    public IHistoryAction? End()
    {
        if (_items.Count == 0) return null;
        var steps = new List<IHistoryAction?>();
        foreach (var item in _items)
        {
            if (_dx == 0 && _dy == 0)
            {
                item.Layer.RestoreState(item.Before);
                continue;
            }
            if (item.StartObject != null)
            {
                steps.Add(new ObjectChangeAction("Move", item.Layer, item.StartObject, item.Layer.Object));
            }
            else
            {
                var doc = _host.Document.Model;
                item.Layer.RestoreState(LayerState.Place(item.Content!, item.X + _dx, item.Y + _dy, doc.Width, doc.Height));
                steps.Add(new LayerContentAction("Move", item.Layer, item.Before, item.Layer.SaveState()));
            }
        }
        _items.Clear();
        return steps.Count == 0 ? null : new CompositeAction("Move Layers", steps);
    }
}
