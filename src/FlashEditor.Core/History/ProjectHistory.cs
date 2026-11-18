namespace FlashEditor.Core.History;

/// <summary>
/// One undo history for a whole project. Every page keeps its own <see cref="HistoryManager"/> (what was done in that page, and whether the page
/// has unsaved changes), and this timeline remembers the order of all steps across the pages: Undo takes back the newest step of the whole
/// project, whichever page it was made in. The project keeps at most <see cref="MaxSteps"/> steps (the oldest are forgotten first).
/// </summary>
public sealed class ProjectHistory
{
    /// <summary>Steps kept by default.</summary>
    public const int DefaultSteps = 30;

    /// <summary>The most steps that can be kept (every step keeps pixel copies in memory).</summary>
    public const int MaxAllowedSteps = 60;

    public const int MinAllowedSteps = 1;

    /// <summary>A number of steps inside the allowed range.</summary>
    public static int ClampSteps(int steps) => Math.Clamp(steps, MinAllowedSteps, MaxAllowedSteps);

    private readonly List<HistoryManager> _steps = new();     // the history that owns each step, oldest first
    private int _maxSteps = DefaultSteps;

    /// <summary>How many steps are applied: 0 = the project as it was opened.</summary>
    public int Position { get; private set; }

    public int Count => _steps.Count;
    public bool CanUndo => Position > 0;
    public bool CanRedo => Position < _steps.Count;

    /// <summary>The history that owns step number <paramref name="index"/> (0 = oldest).</summary>
    public HistoryManager OwnerOf(int index) => _steps[index];

    /// <summary>The action of step number <paramref name="index"/> (0 = oldest).</summary>
    public IHistoryAction ActionAt(int index)
    {
        var owner = _steps[index];
        int inOwner = 0;
        for (int i = 0; i < index; i++) if (ReferenceEquals(_steps[i], owner)) inOwner++;
        return owner.Actions[inOwner];
    }

    /// <summary>How many steps are kept (1..60). Lowering it forgets the oldest steps at once.</summary>
    public int MaxSteps
    {
        get => _maxSteps;
        set
        {
            _maxSteps = ClampSteps(value);
            TrimToLimit();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when a step is added, undone, redone or forgotten.</summary>
    public event EventHandler? Changed;

    /// <summary>True when the newest applied step of the project belongs to <paramref name="owner"/>.</summary>
    internal bool IsNewestStepOf(HistoryManager owner) => Position > 0 && ReferenceEquals(_steps[Position - 1], owner);

    /// <summary>Everything that could have been redone is gone: new work replaces it, in every page.</summary>
    internal void ForgetStepsAfterPosition(HistoryManager newWorkIn)
    {
        if (Position >= _steps.Count) return;
        var forgotten = _steps.GetRange(Position, _steps.Count - Position);
        _steps.RemoveRange(Position, _steps.Count - Position);
        foreach (var other in forgotten.Distinct().Where(h => !ReferenceEquals(h, newWorkIn)))
            other.DiscardRedo();
    }

    /// <summary>Called by a history that just recorded a NEW step.</summary>
    internal void StepRecorded(HistoryManager owner)
    {
        ForgetStepsAfterPosition(owner);
        _steps.Add(owner);
        Position = _steps.Count;
        TrimToLimit();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called by a history that forgot its own oldest step (too much memory).</summary>
    internal void OldestForgottenBy(HistoryManager owner)
    {
        int index = _steps.FindIndex(h => ReferenceEquals(h, owner));
        if (index < 0) return;
        _steps.RemoveAt(index);
        if (index < Position) Position--;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called when a history leaves the project (its page was removed for good): its steps are forgotten.</summary>
    public void Forget(HistoryManager owner)
    {
        for (int i = _steps.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_steps[i], owner)) continue;
            _steps.RemoveAt(i);
            if (i < Position) Position--;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Takes back the newest applied step of the whole project. Returns the history it belonged to (null when there was nothing to undo).</summary>
    public HistoryManager? Undo()
    {
        if (!CanUndo) return null;
        var owner = _steps[Position - 1];
        owner.Undo();
        Position--;
        Changed?.Invoke(this, EventArgs.Empty);
        return owner;
    }

    /// <summary>Does the step that was just taken back again. Returns the history it belonged to.</summary>
    public HistoryManager? Redo()
    {
        if (!CanRedo) return null;
        var owner = _steps[Position];
        owner.Redo();
        Position++;
        Changed?.Invoke(this, EventArgs.Empty);
        return owner;
    }

    /// <summary>Undoes or redoes until <see cref="Position"/> is <paramref name="position"/>. Returns the history of the last step that was touched.</summary>
    public HistoryManager? JumpTo(int position)
    {
        position = Math.Clamp(position, 0, _steps.Count);
        HistoryManager? last = null;
        while (Position > position) { last = _steps[Position - 1]; last.Undo(); Position--; }
        while (Position < position) { last = _steps[Position]; last.Redo(); Position++; }
        if (last != null) Changed?.Invoke(this, EventArgs.Empty);
        return last;
    }

    private void TrimToLimit()
    {
        // Only steps that are applied can be forgotten from the front; the oldest one is always applied unless everything is undone.
        while (_steps.Count > _maxSteps && Position > 0)
        {
            var owner = _steps[0];
            _steps.RemoveAt(0);
            Position--;
            owner.ForgetOldest();
        }
    }
}
