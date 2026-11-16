using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.History;

/// <summary>
/// Undo step for operations that change every layer and/or the canvas size
/// (crop, resize, rotate, flip, canvas size). Old and new layer contents are swapped, not copied.
/// </summary>
public sealed class CanvasChangeAction : IHistoryAction
{
    private readonly Document _document;
    private readonly (int W, int H) _beforeSize;
    private readonly (int W, int H) _afterSize;
    private readonly Dictionary<Layer, LayerState> _before;
    private readonly Dictionary<Layer, LayerState> _after;

    private CanvasChangeAction(string name, Document document,
        (int, int) beforeSize, Dictionary<Layer, LayerState> before,
        (int, int) afterSize, Dictionary<Layer, LayerState> after)
    {
        Name = name;
        _document = document;
        _beforeSize = beforeSize;
        _before = before;
        _afterSize = afterSize;
        _after = after;
    }

    public string Name { get; }

    /// <summary>
    /// Transforms every layer with <paramref name="transform"/>, resizes the canvas to the size of the
    /// results, and returns the matching undo step. Pixels hidden outside the canvas are dropped
    /// (and come back on undo).
    /// </summary>
    /// <param name="objectTransform">
    /// How editable objects follow the change (gets the object and the new canvas size). Null = object
    /// layers are turned into pixels.
    /// </param>
    public static CanvasChangeAction Apply(string name, Document document, Func<PixelBuffer, PixelBuffer> transform,
        Func<LayerObject, int, int, LayerObject>? objectTransform = null)
    {
        var beforeSize = (document.Width, document.Height);
        var before = new Dictionary<Layer, LayerState>();
        var after = new Dictionary<Layer, LayerState>();

        // Pixel layers first: they decide the new canvas size.
        foreach (var layer in document.Layers)
        {
            before[layer] = layer.SaveState();
            if (layer.Object == null || objectTransform == null)
                after[layer] = LayerState.Simple(transform(layer.Pixels));
        }
        var (w, h) = after.Count > 0
            ? (after.Values.First().Pixels.Width, after.Values.First().Pixels.Height)
            : SizeOf(transform(new PixelBuffer(document.Width, document.Height)));

        foreach (var layer in document.Layers)
            if (layer.Object != null && objectTransform != null)
                after[layer] = Layer.RenderState(objectTransform(layer.Object, w, h), w, h);

        var action = new CanvasChangeAction(name, document, beforeSize, before, (w, h), after);
        action.Redo();
        return action;
    }

    /// <summary>
    /// Changes the canvas size without scaling: every layer (including pixels hidden outside the old canvas)
    /// is shifted by (<paramref name="shiftX"/>, <paramref name="shiftY"/>) and placed on a
    /// <paramref name="width"/> x <paramref name="height"/> canvas. Nothing is thrown away: parts that end up
    /// outside stay hidden on the layer. Used by Canvas Size and Reveal All.
    /// </summary>
    public static CanvasChangeAction Reframe(string name, Document document, int width, int height, int shiftX, int shiftY)
    {
        var beforeSize = (document.Width, document.Height);
        var before = new Dictionary<Layer, LayerState>();
        var after = new Dictionary<Layer, LayerState>();

        foreach (var layer in document.Layers)
        {
            before[layer] = layer.SaveState();
            if (layer.Object != null)
            {
                after[layer] = Layer.RenderState(ObjectTransforms.Offset(layer.Object, shiftX, shiftY), width, height);
                continue;
            }
            var (content, x, y) = layer.GetFullContent();
            after[layer] = LayerState.Place(content, x + shiftX, y + shiftY, width, height);
        }

        var action = new CanvasChangeAction(name, document, beforeSize, before, (width, height), after);
        action.Redo();
        return action;
    }

    /// <summary>Transforms a single layer without changing the canvas size (e.g. flip one layer).</summary>
    public static CanvasChangeAction ApplyToLayer(string name, Document document, Layer layer, Func<PixelBuffer, PixelBuffer> transform)
    {
        var size = (document.Width, document.Height);
        var before = new Dictionary<Layer, LayerState> { [layer] = layer.SaveState() };
        var after = new Dictionary<Layer, LayerState> { [layer] = LayerState.Simple(transform(layer.Pixels)) };
        var action = new CanvasChangeAction(name, document, size, before, size, after);
        action.Redo();
        return action;
    }

    private static (int, int) SizeOf(PixelBuffer p) => (p.Width, p.Height);

    public void Undo() => Restore(_beforeSize, _before);
    public void Redo() => Restore(_afterSize, _after);

    private void Restore((int W, int H) size, Dictionary<Layer, LayerState> states)
    {
        foreach (var (layer, state) in states)
            layer.RestoreState(state);
        _document.SetCanvasSize(size.W, size.H);
        _document.Selection = null;
    }
}
