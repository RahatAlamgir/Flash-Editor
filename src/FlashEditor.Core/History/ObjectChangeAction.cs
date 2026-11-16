using FlashEditor.Core.Documents;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.History;

/// <summary>
/// Undo step for editing an object layer (move, resize, rotate, color, text...) or for turning it into
/// pixels (after = null). Only the small object descriptions are stored; pixels are redrawn on undo/redo.
/// Consecutive changes of the same property on the same layer (e.g. dragging a slider) merge into one step.
/// </summary>
public sealed class ObjectChangeAction : IHistoryAction
{
    private readonly Layer _layer;
    private readonly LayerObject _before;
    private LayerObject? _after;
    private readonly string? _mergeKey;

    /// <param name="mergeKey">Changes with the same non-null key in a row become one undo step.</param>
    public ObjectChangeAction(string name, Layer layer, LayerObject before, LayerObject? after, string? mergeKey = null)
    {
        Name = name;
        _layer = layer;
        _before = before;
        _after = after;
        _mergeKey = mergeKey;
    }

    public string Name { get; }

    /// <summary>Applies <paramref name="after"/> to the layer and returns the undo step (null if nothing changed).</summary>
    public static ObjectChangeAction? Apply(string name, Layer layer, LayerObject? after, string? mergeKey = null)
    {
        if (layer.Object is not { } before || Equals(before, after)) return null;
        layer.SetObject(after);
        return new ObjectChangeAction(name, layer, before, after, mergeKey);
    }

    public void Undo() => _layer.SetObject(_before);

    public void Redo() => _layer.SetObject(_after);

    public bool TryMerge(IHistoryAction next)
    {
        if (_mergeKey == null || next is not ObjectChangeAction other) return false;
        if (other._layer != _layer || other._mergeKey != _mergeKey) return false;
        _after = other._after;
        return true;
    }
}
