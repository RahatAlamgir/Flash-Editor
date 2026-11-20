using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Documents;

public class LayerMaskTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "flash-mask-tests-" + Guid.NewGuid().ToString("N"));

    public LayerMaskTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); } catch { /* temp folder */ }
    }

    private static Layer Red(int w = 20, int h = 10)
    {
        var p = new PixelBuffer(w, h);
        p.Fill(new ColorRgba(255, 0, 0));
        return new Layer("Red", p);
    }

    private static Document DocWith(Layer layer)
    {
        var doc = new Document(layer.Width, layer.Height);
        doc.SetLayers(new[] { layer }, layer);
        return doc;
    }

    [Fact]
    public void A_hidden_part_of_a_layer_does_not_show_when_the_picture_is_flattened()
    {
        var layer = Red();
        var mask = new LayerMask(20, 10);
        for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) mask.Alpha[y * 20 + x] = 0;     // hide the left half
        layer.SetMask(mask);
        var flat = Compositor.Flatten(DocWith(layer));
        Assert.Equal(0, flat.GetPixel(3, 3).A);
        Assert.Equal(255, flat.GetPixel(15, 3).A);
        Assert.Equal(255, layer.Pixels.GetPixel(3, 3).A);                  // the layer itself is untouched
    }

    [Fact]
    public void A_half_visible_mask_makes_the_layer_half_see_through()
    {
        var layer = Red();
        layer.SetMask(new LayerMask(20, 10, 128));
        var flat = Compositor.Flatten(DocWith(layer));
        Assert.InRange(flat.GetPixel(5, 5).A, 126, 130);
    }

    [Fact]
    public void A_switched_off_mask_changes_nothing()
    {
        var layer = Red();
        layer.SetMask(new LayerMask(20, 10, 0));
        layer.MaskEnabled = false;
        Assert.Equal(255, Compositor.Flatten(DocWith(layer)).GetPixel(5, 5).A);
        layer.MaskEnabled = true;
        Assert.Equal(0, Compositor.Flatten(DocWith(layer)).GetPixel(5, 5).A);
    }

    [Fact]
    public void The_merged_color_sampler_sees_the_mask_too()
    {
        var layer = Red();
        layer.SetMask(new LayerMask(20, 10, 0));
        Assert.Equal(0, Compositor.SampleMerged(DocWith(layer), 5, 5).A);
    }

    [Fact]
    public void A_mask_must_have_the_size_of_the_canvas()
        => Assert.Throws<ArgumentException>(() => Red().SetMask(new LayerMask(5, 5)));

    [Fact]
    public void A_mask_from_a_selection_shows_or_hides_the_selected_area()
    {
        var selection = Selection.FromRectangle(new PixelRect(2, 2, 6, 4), 20, 10)!;
        var reveal = LayerMask.FromSelection(selection, revealSelected: true);
        Assert.Equal(255, reveal.Alpha[3 * 20 + 3]);
        Assert.Equal(0, reveal.Alpha[0]);
        var hide = LayerMask.FromSelection(selection, revealSelected: false);
        Assert.Equal(0, hide.Alpha[3 * 20 + 3]);
        Assert.Equal(255, hide.Alpha[0]);
    }

    [Fact]
    public void Hiding_a_selection_on_an_existing_mask_keeps_what_was_already_hidden()
    {
        var mask = new LayerMask(20, 10);
        mask.Alpha[0] = 0;
        mask.ApplySelection(Selection.FromRectangle(new PixelRect(5, 5, 3, 3), 20, 10)!, reveal: false);
        Assert.Equal(0, mask.Alpha[0]);
        Assert.Equal(0, mask.Alpha[6 * 20 + 6]);
        Assert.Equal(255, mask.Alpha[9 * 20 + 19]);
        mask.ApplySelection(Selection.FromRectangle(new PixelRect(5, 5, 1, 1), 20, 10)!, reveal: true);
        Assert.Equal(255, mask.Alpha[5 * 20 + 5]);
    }

    [Fact]
    public void Inverting_turns_hidden_into_visible()
    {
        var mask = new LayerMask(4, 1);
        mask.Alpha[0] = 0; mask.Alpha[1] = 200;
        mask.Invert();
        Assert.Equal(new byte[] { 255, 55, 0, 0 }, mask.Alpha);
    }

    [Fact]
    public void Adding_a_mask_can_be_undone_and_redone()
    {
        var layer = Red();
        var action = MaskStateAction.Record("Add Mask", layer, () => layer.SetMask(new LayerMask(20, 10, 0)));
        Assert.NotNull(layer.Mask);
        action.Undo();
        Assert.Null(layer.Mask);
        action.Redo();
        Assert.Equal(0, layer.Mask!.Alpha[0]);
    }

    [Fact]
    public void Painting_on_the_mask_can_be_undone()
    {
        var layer = Red();
        layer.SetMask(new LayerMask(20, 10));
        var region = new PixelRect(2, 2, 4, 4);
        var before = layer.Mask!.CopyRegion(region);
        for (int y = 2; y < 6; y++) for (int x = 2; x < 6; x++) layer.Mask.Alpha[y * 20 + x] = 0;
        var action = new MaskPaintAction("Mask Brush", layer, region, before, layer.Mask.CopyRegion(region));
        action.Undo();
        Assert.Equal(255, layer.Mask.Alpha[3 * 20 + 3]);
        action.Redo();
        Assert.Equal(0, layer.Mask.Alpha[3 * 20 + 3]);
    }

    [Fact]
    public void A_duplicated_layer_has_its_own_copy_of_the_mask()
    {
        var layer = Red();
        layer.SetMask(new LayerMask(20, 10, 100));
        var copy = layer.Duplicate("copy");
        copy.Mask!.Alpha[0] = 7;
        Assert.Equal(100, layer.Mask!.Alpha[0]);
        Assert.Equal(100, copy.Mask.Alpha[1]);
    }

    [Fact]
    public void Resizing_the_picture_applies_the_mask_first_and_undo_brings_the_mask_back()
    {
        var layer = Red();
        var mask = new LayerMask(20, 10);
        for (int x = 0; x < 10; x++) for (int y = 0; y < 10; y++) mask.Alpha[y * 20 + x] = 0;
        layer.SetMask(mask);
        var doc = DocWith(layer);

        var action = CanvasChangeAction.Apply("Flip", doc, p => p);           // any change of the canvas
        Assert.Null(layer.Mask);                                                // the mask became part of the pixels
        Assert.Equal(0, layer.Pixels.GetPixel(3, 3).A);
        Assert.Equal(255, layer.Pixels.GetPixel(15, 3).A);

        action.Undo();
        Assert.NotNull(layer.Mask);
        Assert.Equal(0, layer.Mask!.Alpha[3 * 20 + 3]);
        Assert.Equal(255, layer.Pixels.GetPixel(3, 3).A);
    }

    [Fact]
    public void Moving_the_layers_pixels_leaves_the_mask_where_it_is()
    {
        var layer = Red();
        layer.SetMask(new LayerMask(20, 10, 90));
        layer.RestoreState(LayerState.Place(layer.Pixels.Clone(), 3, 0, 20, 10));
        Assert.Equal(90, layer.Mask!.Alpha[0]);
    }

    [Fact]
    public void The_mask_is_saved_with_the_project_and_comes_back()
    {
        var layer = Red();
        var mask = new LayerMask(20, 10);
        mask.Alpha[4 * 20 + 7] = 33;
        layer.SetMask(mask);
        layer.MaskEnabled = false;
        string path = Path.Combine(_folder, "masked.flash");
        ProjectWriter.Save(DocWith(layer), path);

        var loaded = ProjectReader.Open(path).Layers[0];
        Assert.NotNull(loaded.Mask);
        Assert.Equal(33, loaded.Mask!.Alpha[4 * 20 + 7]);
        Assert.Equal(255, loaded.Mask.Alpha[0]);
        Assert.False(loaded.MaskEnabled);
    }

    [Fact]
    public void A_layer_without_a_mask_is_saved_and_opened_as_before()
    {
        string path = Path.Combine(_folder, "plain.flash");
        ProjectWriter.Save(DocWith(Red()), path);
        Assert.Null(ProjectReader.Open(path).Layers[0].Mask);
    }
}
