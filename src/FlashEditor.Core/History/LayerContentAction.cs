using FlashEditor.Core.Documents;

namespace FlashEditor.Core.History;

/// <summary>
/// Undo step that swaps a layer's whole content (including pixels outside the canvas).
/// Used by the Move tool, where content can travel past the canvas edge and back.
/// </summary>
public sealed class LayerContentAction : IHistoryAction
{
    private readonly Layer _layer;
    private readonly LayerState _before;
    private readonly LayerState _after;

    /// <param name="before">A copy of the layer state taken before the change (it must not be live).</param>
    /// <param name="after">The state the layer has now.</param>
    public LayerContentAction(string name, Layer layer, LayerState before, LayerState after)
    {
        Name = name;
        _layer = layer;
        _before = before;
        _after = after;
    }

    public string Name { get; }

    public void Undo() => _layer.RestoreState(_before);
    public void Redo() => _layer.RestoreState(_after);
}
