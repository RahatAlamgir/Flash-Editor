namespace FlashEditor.App.ViewModels;

/// <summary>One row in the History panel.</summary>
/// <param name="Position">History position this row represents (0 = original state).</param>
/// <param name="IsUndone">True for steps after the current state (shown dimmed, can be redone).</param>
public sealed record HistoryItemViewModel(string Name, int Position, bool IsUndone)
{
    public double RowOpacity => IsUndone ? 0.45 : 1.0;
}
