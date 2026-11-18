using FlashEditor.Core.Documents;

namespace FlashEditor.Core.Tests.Documents;

public class LayerOrderTests
{
    // Bottom to top: A, B, C, D. The panel lists them top first: D, C, B, A.
    private static readonly Layer A = new("A", 4, 4), B = new("B", 4, 4), C = new("C", 4, 4), D = new("D", 4, 4);
    private static readonly Layer[] Stack = { A, B, C, D };

    private static string Names(IEnumerable<Layer>? layers) => layers == null ? "(unchanged)" : string.Concat(layers.Select(l => l.Name));

    // A long list only has rows for the part that is on screen: here rows 3..6 of 10 (each 48 high, row 3 starts at 0).
    private static readonly IReadOnlyList<LayerOrder.RowSpan> Visible = new[]
    {
        new LayerOrder.RowSpan(3, 0, 48), new LayerOrder.RowSpan(4, 48, 96), new LayerOrder.RowSpan(5, 96, 144), new LayerOrder.RowSpan(6, 144, 192),
    };

    [Fact]
    public void Below_the_last_visible_row_is_the_gap_after_that_row_not_the_end_of_the_whole_list()
    {
        // This used to send a dragged layer behind everything: the pointer was below the last row that exists on screen.
        Assert.Equal(7, LayerOrder.GapAt(Visible, 185, 10));
        Assert.Equal(7, LayerOrder.GapAt(Visible, 500, 10));
    }

    [Fact]
    public void The_gap_is_the_one_nearest_to_the_pointer_between_visible_rows()
    {
        Assert.Equal(3, LayerOrder.GapAt(Visible, 10, 10));     // upper half of the first visible row
        Assert.Equal(4, LayerOrder.GapAt(Visible, 30, 10));     // lower half of it: gap after it
        Assert.Equal(5, LayerOrder.GapAt(Visible, 80, 10));
        Assert.Equal(6, LayerOrder.GapAt(Visible, 120, 10));
    }

    [Fact]
    public void Below_the_last_row_of_a_short_list_is_the_very_end()
    {
        var all = new[] { new LayerOrder.RowSpan(0, 0, 48), new LayerOrder.RowSpan(1, 48, 96), new LayerOrder.RowSpan(2, 96, 144) };
        Assert.Equal(3, LayerOrder.GapAt(all, 300, 3));
        Assert.Equal(0, LayerOrder.GapAt(Array.Empty<LayerOrder.RowSpan>(), 10, 0));
    }

    [Fact]
    public void Dragging_to_the_top_of_the_list_brings_the_layer_to_the_front()
    {
        Assert.Equal("ACDB", Names(LayerOrder.MoveToGap(Stack, new[] { B }, 0)));
    }

    [Fact]
    public void Dragging_below_the_last_row_sends_the_layer_to_the_back()
    {
        Assert.Equal("DABC", Names(LayerOrder.MoveToGap(Stack, new[] { D }, 4)));
    }

    [Fact]
    public void Dragging_into_a_gap_between_two_rows()
    {
        // list: D C B A ; drop A between D and C (gap 1) -> D A C B -> bottom to top: B C A D
        Assert.Equal("BCAD", Names(LayerOrder.MoveToGap(Stack, new[] { A }, 1)));
    }

    [Fact]
    public void Dropping_where_it_already_is_changes_nothing()
    {
        Assert.Null(LayerOrder.MoveToGap(Stack, new[] { C }, 1)); // gap just above its own row
        Assert.Null(LayerOrder.MoveToGap(Stack, new[] { C }, 2)); // gap just below its own row
        Assert.Null(LayerOrder.MoveToGap(Stack, new[] { D }, 0));
    }

    [Fact]
    public void Several_layers_move_together_and_keep_their_order()
    {
        // list: D C B A ; drag D and B to the bottom -> C A D B -> bottom to top: B D A C
        Assert.Equal("BDAC", Names(LayerOrder.MoveToGap(Stack, new[] { B, D }, 4)));
        // drag A and B to the top -> B A D C -> bottom to top: C D A B
        Assert.Equal("CDAB", Names(LayerOrder.MoveToGap(Stack, new[] { A, B }, 0)));
    }

    [Fact]
    public void A_gap_past_the_end_or_a_layer_that_is_not_there_is_harmless()
    {
        Assert.Equal("DABC", Names(LayerOrder.MoveToGap(Stack, new[] { D }, 99)));
        Assert.Null(LayerOrder.MoveToGap(Stack, new[] { new Layer("X", 4, 4) }, 2));
    }
}
