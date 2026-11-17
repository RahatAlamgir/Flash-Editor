using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Painting;
using Xunit;

namespace FlashEditor.Core.Tests.Imaging;

public class BlendModeTests
{
    private static PixelBuffer Solid(ColorRgba c, int w = 4, int h = 4) { var p = new PixelBuffer(w, h); p.Fill(c); return p; }

    private static ColorRgba Mix(ColorRgba below, ColorRgba above, BlendMode mode, double opacity = 1)
    {
        var dst = Solid(below);
        var src = Solid(above);
        BlendMath.BlendPixel(dst.Data, 0, src.Data, 0, opacity, mode);
        return dst.GetPixel(0, 0);
    }

    [Fact]
    public void Normal_covers_what_is_below()
    {
        Assert.Equal(new ColorRgba(10, 20, 30), Mix(new ColorRgba(200, 200, 200), new ColorRgba(10, 20, 30), BlendMode.Normal));
    }

    [Fact]
    public void Multiply_darkens_and_white_changes_nothing()
    {
        Assert.Equal(new ColorRgba(200, 100, 50), Mix(new ColorRgba(200, 100, 50), new ColorRgba(255, 255, 255), BlendMode.Multiply));
        Assert.Equal(new ColorRgba(0, 0, 0), Mix(new ColorRgba(200, 100, 50), new ColorRgba(0, 0, 0), BlendMode.Multiply));
        var half = Mix(new ColorRgba(200, 200, 200), new ColorRgba(128, 128, 128), BlendMode.Multiply);
        Assert.InRange(half.R, 99, 101);
    }

    [Fact]
    public void Screen_lightens_and_black_changes_nothing()
    {
        Assert.Equal(new ColorRgba(200, 100, 50), Mix(new ColorRgba(200, 100, 50), new ColorRgba(0, 0, 0), BlendMode.Screen));
        Assert.Equal(new ColorRgba(255, 255, 255), Mix(new ColorRgba(200, 100, 50), new ColorRgba(255, 255, 255), BlendMode.Screen));
        Assert.True(Mix(new ColorRgba(100, 100, 100), new ColorRgba(100, 100, 100), BlendMode.Screen).R > 100);
    }

    [Fact]
    public void Darken_lighten_and_difference_pick_what_they_say()
    {
        var below = new ColorRgba(100, 200, 50);
        var above = new ColorRgba(150, 100, 50);
        Assert.Equal(new ColorRgba(100, 100, 50), Mix(below, above, BlendMode.Darken));
        Assert.Equal(new ColorRgba(150, 200, 50), Mix(below, above, BlendMode.Lighten));
        Assert.Equal(new ColorRgba(50, 100, 0), Mix(below, above, BlendMode.Difference));
    }

    [Fact]
    public void Overlay_and_soft_light_keep_mid_grey_and_push_contrast()
    {
        var grey = new ColorRgba(128, 128, 128);
        Assert.InRange(Mix(new ColorRgba(60, 60, 60), grey, BlendMode.Overlay).R, 58, 62);          // a mid grey layer changes nothing
        Assert.InRange(Mix(new ColorRgba(60, 60, 60), grey, BlendMode.SoftLight).R, 58, 62);
        Assert.True(Mix(new ColorRgba(60, 60, 60), new ColorRgba(200, 200, 200), BlendMode.Overlay).R < 60 + 80);
        Assert.True(Mix(new ColorRgba(60, 60, 60), new ColorRgba(200, 200, 200), BlendMode.SoftLight).R > 60);   // a light layer lightens
        Assert.True(Mix(new ColorRgba(60, 60, 60), new ColorRgba(20, 20, 20), BlendMode.HardLight).R < 60);      // a dark layer darkens
    }

    [Fact]
    public void Opacity_and_transparency_are_respected()
    {
        var half = Mix(new ColorRgba(200, 200, 200), new ColorRgba(0, 0, 0), BlendMode.Multiply, 0.5);
        Assert.InRange(half.R, 99, 101);                                          // halfway between 200 and 0
        var transparentBelow = new PixelBuffer(1, 1);                              // nothing to blend with: just the layer
        var src = Solid(new ColorRgba(10, 20, 30));
        BlendMath.BlendPixel(transparentBelow.Data, 0, src.Data, 0, 1, BlendMode.Multiply);
        Assert.Equal(new ColorRgba(10, 20, 30), transparentBelow.GetPixel(0, 0));
    }

    [Fact]
    public void A_flattened_document_uses_the_blend_mode_of_each_layer()
    {
        var doc = new Document(8, 8);
        var back = new Layer("Back", Solid(new ColorRgba(200, 100, 50), 8, 8));
        var top = new Layer("Top", Solid(new ColorRgba(128, 128, 128), 8, 8)) { BlendMode = BlendMode.Multiply };
        doc.SetLayers(new List<Layer> { back, top }, top);

        var flat = Compositor.Flatten(doc);
        Assert.InRange(flat.GetPixel(3, 3).R, 99, 101);
        Assert.Equal(flat.GetPixel(3, 3), Compositor.SampleMerged(doc, 3, 3));

        top.BlendMode = BlendMode.Normal;
        Assert.Equal(new ColorRgba(128, 128, 128), Compositor.Flatten(doc).GetPixel(3, 3));
    }

    [Fact]
    public void Redrawing_one_region_matches_a_full_flatten_and_leaves_the_rest()
    {
        var doc = new Document(20, 20);
        var back = new Layer("Back", Solid(new ColorRgba(200, 100, 50), 20, 20));
        var top = new Layer("Top", Solid(new ColorRgba(60, 120, 240), 20, 20)) { BlendMode = BlendMode.Screen, Opacity = 0.7 };
        doc.SetLayers(new List<Layer> { back, top }, top);

        var full = Compositor.Flatten(doc);
        var target = Solid(new ColorRgba(1, 2, 3), 20, 20);
        Compositor.FlattenRegion(doc, new PixelRect(5, 5, 6, 6), target);
        Assert.Equal(full.GetPixel(7, 7), target.GetPixel(7, 7));
        Assert.Equal(new ColorRgba(1, 2, 3), target.GetPixel(0, 0));              // outside the region: untouched
        Assert.Equal(new ColorRgba(1, 2, 3), target.GetPixel(11, 11));
    }

    [Fact]
    public void The_blend_mode_is_saved_with_the_project_and_copied_with_the_layer()
    {
        var doc = new Document(6, 6);
        var layer = new Layer("A", Solid(new ColorRgba(1, 2, 3), 6, 6)) { BlendMode = BlendMode.SoftLight };
        doc.SetLayers(new List<Layer> { layer }, layer);
        Assert.Equal(BlendMode.SoftLight, layer.Duplicate("copy").BlendMode);
        Assert.Equal(BlendMode.SoftLight, DocumentCopy.Copy(doc).Layers[0].BlendMode);

        var path = Path.Combine(Path.GetTempPath(), "blend-" + Guid.NewGuid().ToString("N") + ".flash");
        try
        {
            ProjectWriter.Save(doc, path);
            Assert.Equal(BlendMode.SoftLight, ProjectReader.Open(path).Layers[0].BlendMode);
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            using var text = new StreamReader(zip.GetEntry("project.json")!.Open());
            Assert.Contains("\"SoftLight\"", text.ReadToEnd());                   // readable name, not a number
        }
        finally { File.Delete(path); }
    }
}
