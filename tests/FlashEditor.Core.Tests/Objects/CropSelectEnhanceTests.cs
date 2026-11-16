using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Objects;

public class CropSelectEnhanceTests
{
    public CropSelectEnhanceTests() => FakeObjectRenderer.Install();

    [Fact]
    public void Cropping_a_picture_object_cuts_its_source_and_keeps_it_an_object()
    {
        var source = TestImages.Numbered(100, 50);
        var layer = new Layer("Photo", 200, 200);
        layer.SetObject(new ImageObject(new ObjectBox(10, 10, 200, 100), source)); // shown at 2x

        layer.RestoreState(LayerCrop.ToRectangle(layer, new PixelRect(10, 10, 100, 50))); // keep the top-left quarter
        var image = Assert.IsType<ImageObject>(layer.Object);
        Assert.Equal(new ObjectBox(10, 10, 100, 50), image.Box);
        Assert.Equal((50, 25), (image.Source.Width, image.Source.Height));
        Assert.Equal(source.GetPixel(0, 0), image.Source.GetPixel(0, 0));
    }

    [Fact]
    public void Cropping_a_painted_layer_to_a_selection_keeps_only_that_shape()
    {
        var layer = new Layer("Paint", TestImages.Solid(40, 40, ColorRgba.Black));
        var selection = Selection.FromEllipse(new PixelRect(10, 10, 20, 20), 40, 40)!;
        layer.RestoreState(LayerCrop.ToSelection(layer, selection));

        var image = Assert.IsType<ImageObject>(layer.Object);
        Assert.Equal(new ObjectBox(10, 10, 20, 20), image.Box);
        Assert.Equal(0, image.Source.GetPixel(0, 0).A);     // corner outside the circle is gone
        Assert.Equal(255, image.Source.GetPixel(10, 10).A); // center kept
    }

    [Fact]
    public void Quick_select_spreads_over_similar_color_and_stops_at_edges()
    {
        var image = TestImages.Solid(60, 20, new ColorRgba(200, 30, 30));
        for (int y = 0; y < 20; y++)
            for (int x = 30; x < 60; x++)
                image.SetPixel(x, y, new ColorRgba(30, 30, 200)); // blue right half
        var mask = new byte[60 * 20];

        QuickSelect.AddDab(image, mask, 10, 10, 4, 32);
        Assert.Equal(255, mask[10 * 60 + 25]); // grew to the right, still red
        Assert.Equal(0, mask[10 * 60 + 35]);   // stopped at the blue edge
    }

    [Fact]
    public void Enhance_makes_a_soft_edge_sharper_and_leaves_flat_areas()
    {
        var image = new PixelBuffer(160, 1);
        for (int x = 0; x < 160; x++)
        {
            byte v = (byte)Math.Clamp(60 + (x - 95) * 13, 60, 190); // a soft ramp from dark to light (x 95..105)
            image.SetPixel(x, 0, new ColorRgba(v, v, v));
        }
        var effect = new EnhanceEffect();
        var values = effect.CreateDefaultValues();
        values["denoise"] = 0;
        var result = effect.Apply(image, values);

        Assert.True(result.GetPixel(105, 0).R - result.GetPixel(94, 0).R > image.GetPixel(105, 0).R - image.GetPixel(94, 0).R,
            "the edge has more contrast");
        Assert.Equal(image.GetPixel(2, 0), result.GetPixel(2, 0)); // flat area far from the edge unchanged
    }
}
