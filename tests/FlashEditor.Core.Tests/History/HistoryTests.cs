using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.History;

public class HistoryTests
{
    private static (Document Doc, Layer Layer) NewDocument()
    {
        var doc = Document.CreateBlank(10, 10, ColorRgba.White);
        return (doc, doc.Layers[0]);
    }

    private static void PaintPixel(Document doc, Layer layer, int x, int y, ColorRgba color)
    {
        var before = layer.Pixels.Clone();
        layer.Pixels.SetPixel(x, y, color);
        doc.History.Push(PixelChangeAction.Create("Paint", layer, before, new PixelRect(x, y, 1, 1))!);
    }

    [Fact]
    public void Undo_and_redo_pixel_change()
    {
        var (doc, layer) = NewDocument();
        PaintPixel(doc, layer, 3, 3, ColorRgba.Black);

        doc.History.Undo();
        Assert.Equal(ColorRgba.White, layer.Pixels.GetPixel(3, 3));

        doc.History.Redo();
        Assert.Equal(ColorRgba.Black, layer.Pixels.GetPixel(3, 3));
    }

    [Fact]
    public void New_action_discards_redo_steps()
    {
        var (doc, layer) = NewDocument();
        PaintPixel(doc, layer, 0, 0, ColorRgba.Black);
        PaintPixel(doc, layer, 1, 0, ColorRgba.Black);
        doc.History.Undo();
        PaintPixel(doc, layer, 2, 0, ColorRgba.Black);

        Assert.Equal(2, doc.History.Actions.Count);
        Assert.False(doc.History.CanRedo);
    }

    [Fact]
    public void JumpTo_moves_several_steps()
    {
        var (doc, layer) = NewDocument();
        for (int i = 0; i < 5; i++) PaintPixel(doc, layer, i, 0, ColorRgba.Black);

        doc.History.JumpTo(2);
        Assert.Equal(ColorRgba.Black, layer.Pixels.GetPixel(1, 0));
        Assert.Equal(ColorRgba.White, layer.Pixels.GetPixel(2, 0));

        doc.History.JumpTo(5);
        Assert.Equal(ColorRgba.Black, layer.Pixels.GetPixel(4, 0));
    }

    [Fact]
    public void Unchanged_pixels_create_no_action()
    {
        var (_, layer) = NewDocument();
        var before = layer.Pixels.Clone();
        Assert.Null(PixelChangeAction.Create("Nothing", layer, before, layer.Pixels.Bounds));
    }

    [Fact]
    public void Dirty_flag_follows_saved_position()
    {
        var (doc, layer) = NewDocument();
        Assert.False(doc.History.IsDirty);
        PaintPixel(doc, layer, 0, 0, ColorRgba.Black);
        Assert.True(doc.History.IsDirty);
        doc.History.MarkSaved();
        Assert.False(doc.History.IsDirty);
        doc.History.Undo();
        Assert.True(doc.History.IsDirty);
    }

    [Fact]
    public void Opacity_changes_merge_into_one_step()
    {
        var (doc, layer) = NewDocument();
        doc.History.Push(new LayerPropertyAction(layer, LayerProperty.Opacity, 1.0, 0.9));
        doc.History.Push(new LayerPropertyAction(layer, LayerProperty.Opacity, 0.9, 0.5));
        layer.Opacity = 0.5;

        Assert.Single(doc.History.Actions);
        doc.History.Undo();
        Assert.Equal(1.0, layer.Opacity);
    }

    [Fact]
    public void Layer_structure_undo_restores_deleted_layer()
    {
        var (doc, layer) = NewDocument();
        var second = new Layer("Second", 10, 10);
        doc.History.Push(LayerStructureAction.Record("Add", doc, () => doc.SetLayers(new[] { layer, second }, second)));
        doc.History.Push(LayerStructureAction.Record("Delete", doc, () => doc.SetLayers(new[] { layer }, layer)));

        doc.History.Undo();
        Assert.Equal(2, doc.Layers.Count);
        Assert.Same(second, doc.ActiveLayer);
    }

    [Fact]
    public void Canvas_change_undo_restores_size_and_pixels()
    {
        var (doc, layer) = NewDocument();
        var original = layer.Pixels;
        doc.History.Push(CanvasChangeAction.Apply("Crop", doc, p => p.Crop(new PixelRect(0, 0, 4, 3))));
        Assert.Equal((4, 3), (doc.Width, doc.Height));

        doc.History.Undo();
        Assert.Equal((10, 10), (doc.Width, doc.Height));
        Assert.Same(original, layer.Pixels);
    }

    [Fact]
    public void Oldest_steps_are_dropped_past_the_limit()
    {
        var (doc, layer) = NewDocument();
        doc.History.MaxActions = 3;
        for (int i = 0; i < 5; i++) PaintPixel(doc, layer, i, 0, ColorRgba.Black);
        Assert.Equal(3, doc.History.Actions.Count);
        Assert.Equal(3, doc.History.Position);
    }
}
