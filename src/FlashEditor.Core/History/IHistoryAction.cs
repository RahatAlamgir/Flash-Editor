namespace FlashEditor.Core.History;

/// <summary>
/// One undoable step. An action is pushed to history <b>after</b> its change has already been made,
/// so <see cref="Redo"/> is only called after an <see cref="Undo"/>.
/// </summary>
public interface IHistoryAction
{
    /// <summary>Text shown in the History panel, e.g. "Brush" or "Gaussian Blur".</summary>
    string Name { get; }

    void Undo();
    void Redo();

    /// <summary>
    /// Lets a new action fold itself into this one (e.g. many small opacity changes from dragging
    /// a slider become a single history step). Return true if merged.
    /// </summary>
    bool TryMerge(IHistoryAction next) => false;

    /// <summary>
    /// Roughly how much memory this step keeps alive (copies of pixels for undo). The history drops its oldest
    /// steps when the total gets too large, so a few edits of a huge picture cannot use up all the memory.
    /// Small steps (names, opacity, objects) can leave this at 0.
    /// </summary>
    long SizeInBytes => 0;
}
