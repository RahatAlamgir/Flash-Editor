namespace FlashEditor.Core.Documents;

/// <summary>Reordering layers by dragging them in the Layers panel (which lists the top layer first).</summary>
public static class LayerOrder
{
    /// <summary>One row of the Layers panel that is on screen: its place in the list and where it is vertically.</summary>
    public readonly record struct RowSpan(int Index, double Top, double Bottom);

    /// <summary>
    /// The gap of the list the pointer at height <paramref name="y"/> is closest to (0 = above the first row, <paramref name="rowCount"/> = below
    /// the last row), looking only at the rows that are on screen (a long list only creates the rows you can see). Below the last row you can
    /// see is the gap after THAT row, not the end of the whole list: the list may go on below the visible part.
    /// </summary>
    public static int GapAt(IReadOnlyList<RowSpan> visibleRows, double y, int rowCount)
    {
        if (visibleRows.Count == 0) return 0;
        foreach (var row in visibleRows.OrderBy(r => r.Index))
            if (y < row.Top + (row.Bottom - row.Top) / 2) return row.Index;
        return Math.Min(rowCount, visibleRows.Max(r => r.Index) + 1);
    }

    /// <summary>
    /// The layer stack (bottom to top) after dragging <paramref name="moved"/> to the gap number
    /// <paramref name="gap"/> of the panel's list: 0 = above the top layer, 1 = between the first and second row,
    /// ... , Count = below the bottom layer. The dragged layers keep their order relative to each other.
    /// Returns null when the order would not change.
    /// </summary>
    public static List<Layer>? MoveToGap(IReadOnlyList<Layer> bottomToTop, IReadOnlyCollection<Layer> moved, int gap)
    {
        var topDown = bottomToTop.Reverse().ToList();
        gap = Math.Clamp(gap, 0, topDown.Count);

        // Where the gap is once the dragged layers are taken out of the list.
        int insertAt = topDown.Take(gap).Count(layer => !moved.Contains(layer));
        var dragged = topDown.Where(moved.Contains).ToList();
        if (dragged.Count == 0) return null;

        var result = topDown.Where(layer => !moved.Contains(layer)).ToList();
        result.InsertRange(insertAt, dragged);
        if (result.SequenceEqual(topDown)) return null;

        result.Reverse();
        return result;
    }
}
