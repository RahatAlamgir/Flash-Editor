using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Painting;

namespace FlashEditor.Core.Tests.Documents;

public class AdjustmentTests
{
    private static PixelBuffer Solid(ColorRgba c, int w = 4, int h = 4) { var p = new PixelBuffer(w, h); p.Fill(c); return p; }

    private static ColorRgba Adjusted(ColorRgba c, AdjustmentStyle a, double strength = 1)
    {
        byte b = c.B, g = c.G, r = c.R;
        a.Apply(ref b, ref g, ref r, strength);
        return new ColorRgba(r, g, b, c.A);
    }

    [Fact]
    public void A_neutral_adjustment_changes_nothing()
    {
        var c = new ColorRgba(12, 200, 99);
        Assert.Equal(c, Adjusted(c, AdjustmentStyle.Neutral));
        Assert.True(AdjustmentStyle.Neutral.IsNeutral);
        Assert.False(new AdjustmentStyle(Hue: 1).IsNeutral);
    }

    [Fact]
    public void Brightness_moves_all_channels_and_contrast_pushes_away_from_grey()
    {
        var lighter = Adjusted(new ColorRgba(100, 100, 100), new AdjustmentStyle(Brightness: 20));
        Assert.InRange(lighter.R, 124, 128);                                              // +20 lifts by a tenth of the range
        var darker = Adjusted(new ColorRgba(100, 100, 100), new AdjustmentStyle(Brightness: -20));
        Assert.InRange(darker.R, 73, 76);

        Assert.True(Adjusted(new ColorRgba(200, 200, 200), new AdjustmentStyle(Contrast: 50)).R > 200);
        Assert.True(Adjusted(new ColorRgba(50, 50, 50), new AdjustmentStyle(Contrast: 50)).R < 50);
        Assert.InRange(Adjusted(new ColorRgba(128, 128, 128), new AdjustmentStyle(Contrast: 80)).R, 126, 130);   // mid grey stays
        var flat = Adjusted(new ColorRgba(255, 255, 255), new AdjustmentStyle(Contrast: -100));
        Assert.InRange(flat.R, 126, 130);                                                                         // fully flat = grey
    }

    [Fact]
    public void Saturation_and_hue_change_the_color_not_the_brightness_much()
    {
        var grey = Adjusted(new ColorRgba(200, 100, 50), new AdjustmentStyle(Saturation: -100));
        Assert.True(Math.Abs(grey.R - grey.G) <= 1 && Math.Abs(grey.G - grey.B) <= 1);
        var vivid = Adjusted(new ColorRgba(180, 120, 100), new AdjustmentStyle(Saturation: 80));
        Assert.True(vivid.R - vivid.B > 180 - 100);

        var turned = Adjusted(new ColorRgba(255, 0, 0), new AdjustmentStyle(Hue: 120));                          // red -> green
        Assert.True(turned.G > 250 && turned.R < 5 && turned.B < 5);
        Assert.Equal(new ColorRgba(255, 0, 0), Adjusted(new ColorRgba(255, 0, 0), new AdjustmentStyle(Hue: 360 - 360)));
    }

    [Fact]
    public void Strength_mixes_with_the_original_and_transparent_pixels_stay_transparent()
    {
        var half = Adjusted(new ColorRgba(100, 100, 100), new AdjustmentStyle(Brightness: 40), 0.5);
        Assert.InRange(half.R, 124, 128);                                         // halfway to 200
        var buffer = new PixelBuffer(2, 1);
        buffer.SetPixel(0, 0, new ColorRgba(100, 100, 100));
        new AdjustmentStyle(Brightness: 40).Apply(buffer, buffer.Bounds);
        Assert.True(buffer.GetPixel(0, 0).R > 100);
        Assert.Equal(new ColorRgba(0, 0, 0, 0), buffer.GetPixel(1, 0));
    }

    [Fact]
    public void An_adjustment_layer_changes_everything_below_it_and_nothing_above()
    {
        var doc = new Document(6, 6);
        var back = new Layer("Back", Solid(new ColorRgba(100, 100, 100), 6, 6));
        var adjust = new Layer("Adjust", 6, 6) { Adjustment = new AdjustmentStyle(Brightness: 20) };
        var top = new Layer("Top", Solid(new ColorRgba(10, 20, 30), 6, 6));
        doc.SetLayers(new List<Layer> { back, adjust, top }, top);
        Assert.Equal(new ColorRgba(10, 20, 30), Compositor.Flatten(doc).GetPixel(2, 2));          // the layer above is untouched

        top.IsVisible = false;
        var flat = Compositor.Flatten(doc);
        Assert.InRange(flat.GetPixel(2, 2).R, 124, 128);
        Assert.Equal(flat.GetPixel(2, 2), Compositor.SampleMerged(doc, 2, 2));

        var target = Solid(new ColorRgba(1, 2, 3), 6, 6);
        Compositor.FlattenRegion(doc, new PixelRect(1, 1, 2, 2), target);
        Assert.Equal(flat.GetPixel(2, 2), target.GetPixel(2, 2));
        Assert.Equal(new ColorRgba(1, 2, 3), target.GetPixel(5, 5));

        adjust.Opacity = 0.5;                                                                     // opacity = strength
        Assert.InRange(Compositor.Flatten(doc).GetPixel(2, 2).R, 111, 115);
        adjust.IsVisible = false;
        Assert.Equal(new ColorRgba(100, 100, 100), Compositor.Flatten(doc).GetPixel(2, 2));
    }

    [Fact]
    public void An_adjustment_layer_is_saved_copied_and_baked_by_merge_down()
    {
        var doc = new Document(6, 6);
        var back = new Layer("Back", Solid(new ColorRgba(100, 100, 100), 6, 6));
        var adjust = new Layer("Adjust", 6, 6) { Adjustment = new AdjustmentStyle(Brightness: 20, Hue: 10) };
        doc.SetLayers(new List<Layer> { back, adjust }, adjust);
        Assert.True(adjust.IsAdjustment);
        Assert.Equal(adjust.Adjustment, adjust.Duplicate("copy").Adjustment);

        var path = Path.Combine(Path.GetTempPath(), "adjust-" + Guid.NewGuid().ToString("N") + ".flash");
        try
        {
            ProjectWriter.Save(doc, path);
            var back2 = ProjectReader.Open(path);
            Assert.Null(back2.Layers[0].Adjustment);
            Assert.Equal(new AdjustmentStyle(Brightness: 20, Hue: 10), back2.Layers[1].Adjustment);
        }
        finally { File.Delete(path); }

        var below = back.Pixels.Clone();
        Compositor.DrawLayer(below, adjust);                                                      // what Merge Down does
        Assert.True(below.GetPixel(1, 1).R > 100);
    }
}
