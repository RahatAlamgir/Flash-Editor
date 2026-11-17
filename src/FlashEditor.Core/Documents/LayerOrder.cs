namespace FlashEditor.Core.Documents;

/// <summary>Reordering layers by dragging them in the Layers panel (which lists the top layer first).</summary>
public static class LayerOrder
{
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
