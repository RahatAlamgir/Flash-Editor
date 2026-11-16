namespace FlashEditor.Core.History;

/// <summary>Groups several actions into one history step (e.g. "Merge Down" = pixel edit + layer removal).</summary>
public sealed class CompositeAction : IHistoryAction
{
    private readonly IReadOnlyList<IHistoryAction> _steps;

    public CompositeAction(string name, IEnumerable<IHistoryAction?> steps)
    {
        Name = name;
        _steps = steps.OfType<IHistoryAction>().ToArray();
    }

    public string Name { get; }
    public bool IsEmpty => _steps.Count == 0;

    public void Undo()
    {
        for (int i = _steps.Count - 1; i >= 0; i--) _steps[i].Undo();
    }

    public void Redo()
    {
        foreach (var step in _steps) step.Redo();
    }
}
