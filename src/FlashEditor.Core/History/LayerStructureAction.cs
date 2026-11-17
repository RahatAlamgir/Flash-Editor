using FlashEditor.Core.Documents;

namespace FlashEditor.Core.History;

/// <summary>A snapshot of which layers exist, their order, and which one is active.</summary>
public sealed record LayerStack(IReadOnlyList<Layer> Layers, Layer? Active)
{
    public static LayerStack Capture(Document doc) => new(doc.Layers.ToArray(), doc.ActiveLayer);
}

/// <summary>
/// Undo step for adding, deleting, duplicating or reordering layers.
/// Layer objects are kept by reference, so their pixels stay intact while removed.
/// </summary>
public sealed class LayerStructureAction : IHistoryAction
{
    private readonly Document _document;
    private readonly LayerStack _before;
    private readonly LayerStack _after;

    public LayerStructureAction(string name, Document document, LayerStack before, LayerStack after)
    {
        Name = name;
        _document = document;
        _before = before;
        _after = after;
    }

    public string Name { get; }

    /// <summary>Layers that were removed (or are only here for undo) keep their pixels alive.</summary>
    public long SizeInBytes
    {
        get
        {
            var onlyBefore = _before.Layers.Except(_after.Layers);
            var onlyAfter = _after.Layers.Except(_before.Layers);
            return onlyBefore.Concat(onlyAfter).Sum(layer => (long)layer.Pixels.Data.Length);
        }
    }

    /// <summary>Runs <paramref name="change"/> and returns an action describing it.</summary>
    public static LayerStructureAction Record(string name, Document document, Action change)
    {
        var before = LayerStack.Capture(document);
        change();
        return new LayerStructureAction(name, document, before, LayerStack.Capture(document));
    }

    public void Undo() => _document.SetLayers(_before.Layers, _before.Active);
    public void Redo() => _document.SetLayers(_after.Layers, _after.Active);
}
