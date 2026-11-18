namespace FlashEditor.Core.Layout;

/// <summary>Where one tab is along the strip (left and right edge).</summary>
public readonly record struct TabSpan(double Left, double Right);

/// <summary>The maths of dragging a tab left or right along a strip of tabs (projects along the top, pages along the bottom).</summary>
public static class TabOrder
{
    /// <summary>
    /// The gap the pointer at <paramref name="x"/> is closest to: 0 = before the first tab, <paramref name="tabs"/>.Count = after the last one.
    /// A tab counts as passed once the pointer is beyond its middle.
    /// </summary>
    public static int GapAt(IReadOnlyList<TabSpan> tabs, double x)
    {
        for (int i = 0; i < tabs.Count; i++)
            if (x < tabs[i].Left + (tabs[i].Right - tabs[i].Left) / 2) return i;
        return tabs.Count;
    }

    /// <summary>The place a tab at <paramref name="current"/> ends up at when it is dropped into <paramref name="gap"/> (the tab itself is taken out first).</summary>
    public static int IndexAfterMove(int current, int gap) => gap > current ? gap - 1 : gap;
}
