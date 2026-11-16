namespace FlashEditor.Core.History;

/// <summary>
/// Linear undo/redo stack. <see cref="Position"/> is the number of actions currently applied:
/// 0 means "original state", Actions.Count means "everything applied".
/// </summary>
public sealed class HistoryManager
{
    private readonly List<IHistoryAction> _actions = new();
    private int _savedPosition;

    /// <summary>Oldest steps are dropped beyond this count to limit memory use.</summary>
    public int MaxActions { get; set; } = 60;

    public IReadOnlyList<IHistoryAction> Actions => _actions;
    public int Position { get; private set; }

    public bool CanUndo => Position > 0;
    public bool CanRedo => Position < _actions.Count;

    /// <summary>True when the document differs from the last saved (or opened) state.</summary>
    public bool IsDirty => Position != _savedPosition;

    /// <summary>True while an undo/redo is running; changes made then must not be recorded.</summary>
    public bool IsApplying { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Records an action whose change has already been applied.</summary>
    public void Push(IHistoryAction action)
    {
        if (IsApplying) return;

        // A new action discards everything that could have been redone.
        if (Position < _actions.Count)
        {
            _actions.RemoveRange(Position, _actions.Count - Position);
            if (_savedPosition > Position) _savedPosition = -1;
        }

        if (Position > 0 && _actions[Position - 1].TryMerge(action))
        {
            if (_savedPosition == Position) _savedPosition = -1;
            RaiseChanged();
            return;
        }

        _actions.Add(action);
        Position++;

        while (_actions.Count > MaxActions)
        {
            _actions.RemoveAt(0);
            Position--;
            if (_savedPosition >= 0) _savedPosition--;
        }
        RaiseChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        Run(() => _actions[Position - 1].Undo());
        Position--;
        RaiseChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        Run(() => _actions[Position].Redo());
        Position++;
        RaiseChanged();
    }

    /// <summary>Undoes or redoes until <see cref="Position"/> equals <paramref name="position"/>.</summary>
    public void JumpTo(int position)
    {
        position = Math.Clamp(position, 0, _actions.Count);
        if (position == Position) return;
        while (Position > position) { Run(() => _actions[Position - 1].Undo()); Position--; }
        while (Position < position) { Run(() => _actions[Position].Redo()); Position++; }
        RaiseChanged();
    }

    public void MarkSaved()
    {
        _savedPosition = Position;
        RaiseChanged();
    }

    public void Clear()
    {
        _actions.Clear();
        Position = 0;
        _savedPosition = 0;
        RaiseChanged();
    }

    private void Run(Action step)
    {
        IsApplying = true;
        try { step(); }
        finally { IsApplying = false; }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
