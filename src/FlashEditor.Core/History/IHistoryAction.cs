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
}
