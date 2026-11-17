using FlashEditor.Core.Documents;

namespace FlashEditor.Core.Tests.Documents;

public class LayerOrderTests
{
    // Bottom to top: A, B, C, D. The panel lists them top first: D, C, B, A.
    private static readonly Layer A = new("A", 4, 4), B = new("B", 4, 4), C = new("C", 4, 4), D = new("D", 4, 4);
    private static readonly Layer[] Stack = { A, B, C, D };

    private static string Names(IEnumerable<Layer>? layers) => layers == null ? "(unchanged)" : string.Concat(layers.Select(l => l.Name));

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
