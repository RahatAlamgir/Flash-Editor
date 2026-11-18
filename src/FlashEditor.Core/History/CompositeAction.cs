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

    public long SizeInBytes => _steps.Sum(step => step.SizeInBytes);

    /// <summary>Several changes made together (for example one slider on several selected objects) fold into the previous step when every part can.</summary>
    public bool TryMerge(IHistoryAction next)
    {
        if (next is not CompositeAction other || other._steps.Count != _steps.Count || other.Name != Name) return false;
        for (int i = 0; i < _steps.Count; i++)
            if (_steps[i] is not ObjectChangeAction mine || !mine.CanMerge(other._steps[i])) return false;
        for (int i = 0; i < _steps.Count; i++) _steps[i].TryMerge(other._steps[i]);
        return true;
    }

    public void Undo()
    {
        for (int i = _steps.Count - 1; i >= 0; i--) _steps[i].Undo();
    }

    public void Redo()
    {
        foreach (var step in _steps) step.Redo();
    }
}
