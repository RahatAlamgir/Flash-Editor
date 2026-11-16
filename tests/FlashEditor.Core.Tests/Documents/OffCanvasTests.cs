using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Documents;

/// <summary>Pixels moved past the canvas edge must be kept, not cut off.</summary>
public class OffCanvasTests
{
    private static readonly ColorRgba Red = new(255, 0, 0);

    [Fact]
    public void Placing_content_past_the_edge_keeps_the_hidden_part()
    {
        var content = TestImages.Solid(10, 10, Red);
        var state = LayerState.Place(content, 6, 0, 10, 10); // 4 px visible, 6 px hidden on the right

        Assert.Equal(Red, state.Pixels.GetPixel(9, 5));
        Assert.Equal(0, state.Pixels.GetPixel(5, 5).A);
        Assert.NotNull(state.Extended);
        Assert.Equal(16, state.Extended!.Width);
    }

    [Fact]
    public void Content_fully_on_canvas_needs_no_extra_buffer()
    {
        var state = LayerState.Place(TestImages.Solid(4, 4, Red), 2, 2, 10, 10);
        Assert.Null(state.Extended);
    }

    [Fact]
    public void Moving_out_and_back_restores_every_pixel()
    {
        var original = TestImages.Numbered(12, 8);
        var layer = new Layer("L", original.Clone());

        // Move 9 px to the right (most of the layer leaves the canvas)...
        var (content, x, y) = layer.GetFullContent();
        layer.RestoreState(LayerState.Place(content.Clone(), x + 9, y, 12, 8));
        Assert.True(layer.HasHiddenPixels);

        // ...then back again.
        (content, x, y) = layer.GetFullContent();
        layer.RestoreState(LayerState.Place(content.Clone(), x - 9, y, 12, 8));

        Assert.True(original.ContentEquals(layer.Pixels));
        Assert.False(layer.HasHiddenPixels);
    }

    [Fact]
    public void Painting_on_the_canvas_is_kept_in_the_hidden_copy()
    {
        var layer = new Layer("L", 10, 10);
        layer.RestoreState(LayerState.Place(TestImages.Solid(10, 10, Red), 5, 0, 10, 10));

        layer.Pixels.SetPixel(7, 3, ColorRgba.Black);
        layer.NotifyPixelsChanged(new PixelRect(7, 3, 1, 1));

        var (content, x, _) = layer.GetFullContent();
        Assert.Equal(ColorRgba.Black, content.GetPixel(7 - x, 3));
    }

    [Fact]
    public void Reveal_all_grows_the_canvas_to_show_hidden_pixels_and_undo_restores_it()
    {
        var doc = Document.FromImage(TestImages.Solid(10, 10, Red));
        var layer = doc.Layers[0];
        layer.RestoreState(LayerState.Place(TestImages.Solid(10, 10, Red), -4, 0, 10, 10));

        var bounds = layer.VisibleBounds();
        var action = CanvasChangeAction.Reframe("Reveal All", doc, doc.Width - bounds.X, doc.Height, -bounds.X, 0);

        Assert.Equal(14, doc.Width);
        Assert.Equal(Red, layer.Pixels.GetPixel(0, 0));
        Assert.Equal(0, layer.Pixels.GetPixel(13, 0).A);

        action.Undo();
        Assert.Equal(10, doc.Width);
        Assert.True(layer.HasHiddenPixels);
    }

    [Fact]
    public void Layer_content_action_undoes_a_move()
    {
        var layer = new Layer("L", TestImages.Numbered(8, 8));
        var before = layer.SaveState().DeepCopy();
        var (content, _, _) = layer.GetFullContent();
        layer.RestoreState(LayerState.Place(content.Clone(), 6, 6, 8, 8));
        var action = new LayerContentAction("Move", layer, before, layer.SaveState());

        action.Undo();
        Assert.True(TestImages.Numbered(8, 8).ContentEquals(layer.Pixels));
        action.Redo();
        Assert.True(layer.HasHiddenPixels);
    }

    [Fact]
    public void Visible_bounds_ignores_transparent_margins()
    {
        var image = new PixelBuffer(10, 10);
        image.SetPixel(3, 4, Red);
        image.SetPixel(6, 7, Red);
        Assert.Equal(new PixelRect(3, 4, 4, 4), image.VisibleBounds());
        Assert.True(new PixelBuffer(3, 3).VisibleBounds().IsEmpty);
    }
}
