using System.IO.Compression;
using System.Xml;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Pptx;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Tests.Objects;

namespace FlashEditor.Core.Tests.IO;

public class EditablePptxTests
{
    public EditablePptxTests() => FakeObjectRenderer.Install();

    private static Document PageWith(params LayerObject[] objects)
    {
        var page = Document.CreateBlank(1280, 720, ColorRgba.White);
        foreach (var o in objects)
        {
            var layer = new Layer(o.SuggestedLayerName, 1280, 720);
            layer.SetObject(o);
            page.Layers.Add(layer);
        }
        return page;
    }

    private static string Slide1(byte[] pptx, out ZipArchive zip)
    {
        zip = new ZipArchive(new MemoryStream(pptx));
        foreach (var e in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")))
        {
            using var s = e.Open();
            new XmlDocument().Load(s);
        }
        using var reader = new StreamReader(zip.GetEntry("ppt/slides/slide1.xml")!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Text_becomes_a_text_box_with_the_words_and_font()
    {
        var text = new TextObject(new ObjectBox(100, 50, 400, 60), "Hello & <welcome>", "Segoe UI", 40, new ColorRgba(255, 0, 0), Bold: true);
        var xml = Slide1(EditablePptx.Build(new[] { PageWith(text) }), out _);
        Assert.Contains("Hello &amp; &lt;welcome&gt;", xml);
        Assert.Contains("typeface=\"Segoe UI\"", xml);
        Assert.Contains("sz=\"3000\"", xml);          // 40 px = 30 pt
        Assert.Contains("b=\"1\"", xml);
        Assert.Contains("FF0000", xml);
        Assert.Contains("txBox=\"1\"", xml);
    }

    [Fact]
    public void Simple_shapes_become_powerpoint_shapes_with_position_rotation_and_colors()
    {
        var rect = new ShapeObject(new ObjectBox(96, 192, 192, 96, 30), ShapeKind.Rectangle, new ColorRgba(0, 128, 255), new ColorRgba(0, 0, 0), 4, CornerRadius: 24);
        var star = new ShapeObject(new ObjectBox(10, 10, 100, 100), ShapeKind.Star, new ColorRgba(255, 200, 0), null, 0, Points: 5);
        var xml = Slide1(EditablePptx.Build(new[] { PageWith(rect, star) }), out _);
        Assert.Contains("prst=\"roundRect\"", xml);
        Assert.Contains("fmla=\"val 25000\"", xml);   // radius 24 of the shorter side 96
        Assert.Contains("<a:off x=\"914400\" y=\"1828800\"/>", xml);
        Assert.Contains("rot=\"1800000\"", xml);      // 30 degrees
        Assert.Contains("0080FF", xml);
        Assert.Contains("prst=\"star5\"", xml);
        Assert.Contains("<a:ln><a:noFill/></a:ln>", xml);
    }

    [Fact]
    public void A_shape_with_a_shadow_is_placed_as_a_picture()
    {
        var shape = new ShapeObject(new ObjectBox(100, 100, 200, 200), ShapeKind.Ellipse, new ColorRgba(255, 0, 0), null, 0) { Shadow = ShadowStyle.Default };
        var xml = Slide1(EditablePptx.Build(new[] { PageWith(shape) }), out var zip);
        Assert.DoesNotContain("prst=\"ellipse\"", xml);
        Assert.Contains("<p:pic>", xml);
        Assert.Contains(zip.Entries, e => e.FullName.StartsWith("ppt/media/"));
    }

    [Fact]
    public void A_placed_picture_stays_a_picture_with_its_own_box()
    {
        var image = new ImageObject(new ObjectBox(64, 32, 320, 160), TestImages.Numbered(40, 20), "Photo");
        var xml = Slide1(EditablePptx.Build(new[] { PageWith(image) }), out var zip);
        Assert.Contains("name=\"Photo\"", xml);
        Assert.Contains($"<a:off x=\"{64 * 9525}\" y=\"{32 * 9525}\"/>", xml);
        Assert.Contains($"<a:ext cx=\"{320 * 9525}\" cy=\"{160 * 9525}\"/>", xml);
        Assert.Single(zip.Entries, e => e.FullName.StartsWith("ppt/media/"));
    }

    [Fact]
    public void A_page_with_an_adjustment_layer_becomes_one_picture()
    {
        var page = PageWith(new TextObject(new ObjectBox(10, 10, 100, 30), "Hi", "Arial", 20, ColorRgba.Black));
        var adjust = new Layer("Brightness", 1280, 720);
        adjust.Adjustment = new AdjustmentStyle(Brightness: 20);
        page.Layers.Add(adjust);
        var xml = Slide1(EditablePptx.Build(new[] { page }), out _);
        Assert.DoesNotContain("txBox", xml);
        Assert.Contains("<p:pic>", xml);
    }
}
