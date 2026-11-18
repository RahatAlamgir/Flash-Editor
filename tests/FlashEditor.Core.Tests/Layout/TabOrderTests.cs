using FlashEditor.Core.Layout;

namespace FlashEditor.Core.Tests.Layout;

public class TabOrderTests
{
    // Four tabs, 100 wide each: 0-100, 100-200, 200-300, 300-400.
    private static readonly IReadOnlyList<TabSpan> Tabs = new[] { new TabSpan(0, 100), new TabSpan(100, 200), new TabSpan(200, 300), new TabSpan(300, 400) };

    [Theory]
    [InlineData(-50, 0)]      // left of everything
    [InlineData(20, 0)]       // left half of the first tab
    [InlineData(70, 1)]       // right half of the first tab: after it
    [InlineData(150, 2)]      // the middle of the second tab is passed
    [InlineData(260, 3)]
    [InlineData(399, 4)]
    [InlineData(900, 4)]      // right of everything
    public void The_gap_is_found_from_the_middle_of_each_tab(double x, int gap)
        => Assert.Equal(gap, TabOrder.GapAt(Tabs, x));

    [Fact]
    public void No_tabs_means_gap_zero()
        => Assert.Equal(0, TabOrder.GapAt(Array.Empty<TabSpan>(), 50));

    [Theory]
    [InlineData(2, 0, 0)]     // tab 2 to the very front
    [InlineData(2, 1, 1)]
    [InlineData(2, 2, 2)]     // the gap just before itself: stays
    [InlineData(2, 3, 2)]     // the gap just after itself: stays
    [InlineData(2, 4, 3)]     // to the very end
    [InlineData(0, 4, 3)]
    [InlineData(3, 0, 0)]
    public void The_new_place_of_a_dragged_tab(int current, int gap, int expected)
        => Assert.Equal(expected, TabOrder.IndexAfterMove(current, gap));

    [Fact]
    public void Dragging_a_tab_across_its_neighbours_moves_it_one_step_at_a_time()
    {
        // Tab 1 is dragged to the right: it only moves once the pointer passes the middle of tab 2.
        int current = 1;
        Assert.Equal(1, TabOrder.IndexAfterMove(current, TabOrder.GapAt(Tabs, 210)));     // still on the left half of tab 2: stays
        Assert.Equal(2, TabOrder.IndexAfterMove(current, TabOrder.GapAt(Tabs, 260)));     // past its middle: moves to place 2
    }
}
