using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Svg;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.IO;

public class SvgFileTests
{
    [Fact]
    public void Basic_shapes_and_text_are_read_as_editable_objects()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100">
              <rect x="10" y="20" width="50" height="30" fill="#ff0000" rx="4"/>
              <circle cx="100" cy="50" r="20" fill="blue" stroke="black" stroke-width="2"/>
              <polygon points="0,0 10,0 5,10" fill="rgb(0,255,0)"/>
              <text x="20" y="90" font-size="20" fill="#000000">Hello</text>
            </svg>
            """;
        var (w, h, objects) = SvgFile.Parse(svg);
        Assert.Equal((200, 100), (w, h));
        Assert.Equal(4, objects.Count);
        var rect = Assert.IsType<ShapeObject>(objects[0].Object);
        Assert.Equal(ShapeKind.Rectangle, rect.Shape);
        Assert.Equal(10, rect.Box.X);
        Assert.Equal(50, rect.Box.Width);
        Assert.Equal(new ColorRgba(255, 0, 0), rect.Fill);
        var circle = Assert.IsType<ShapeObject>(objects[1].Object);
        Assert.Equal(ShapeKind.Ellipse, circle.Shape);
        Assert.Equal(40, circle.Box.Width);
        Assert.Equal(2, circle.StrokeWidth);
        Assert.IsType<TextObject>(objects[3].Object);
        Assert.Equal("Hello", ((TextObject)objects[3].Object).Text);
    }

    [Fact]
    public void A_viewBox_scales_the_picture_to_its_size()
    {
        var (w, h, objects) = SvgFile.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"200\" viewBox=\"0 0 100 100\"><rect x=\"10\" y=\"10\" width=\"20\" height=\"20\"/></svg>");
        Assert.Equal((200, 200), (w, h));
        var rect = (ShapeObject)objects[0].Object;
        Assert.Equal(20, rect.Box.X);
        Assert.Equal(40, rect.Box.Width);
    }

    [Fact]
    public void Paths_with_curves_become_polygons()
    {
        var (_, _, objects) = SvgFile.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"50\" height=\"50\"><path d=\"M 5 5 L 45 5 C 45 25 25 45 5 45 Z\" fill=\"red\"/></svg>");
        var shape = Assert.IsType<ShapeObject>(Assert.Single(objects).Object);
        Assert.NotNull(shape.Vertices);
        Assert.True(shape.Vertices!.Count > 4);
    }

    [Fact]
    public void Saving_and_opening_keeps_shapes_and_text()
    {
        var doc = new Document(120, 80);
        var back = new Layer("Background", 120, 80);
        var rect = new Layer("Box", 120, 80);
        rect.RestoreState(new LayerState(rect.Pixels, null, 0, 0, new ShapeObject(new ObjectBox(10, 10, 40, 30), ShapeKind.Rectangle, new ColorRgba(10, 200, 30), null, 0)));
        var text = new Layer("Words", 120, 80);
        text.RestoreState(new LayerState(text.Pixels, null, 0, 0, new TextObject(new ObjectBox(5, 50, 100, 24), "Hi there", "Arial", 18, new ColorRgba(0, 0, 0), false, false, TextAlign.Left)));
        doc.SetLayers(new[] { back, rect, text }, text);

        string svg = SvgFile.Export(doc);
        Assert.Contains("<rect", svg);
        Assert.Contains("Hi there", svg);

        var (w, h, objects) = SvgFile.Parse(svg);
        Assert.Equal((120, 80), (w, h));
        Assert.Equal(2, objects.Count);
        Assert.Equal(new ColorRgba(10, 200, 30), ((ShapeObject)objects[0].Object).Fill);
        Assert.Equal("Hi there", ((TextObject)objects[1].Object).Text);
    }

    [Fact]
    public void A_file_that_is_not_svg_is_refused()
        => Assert.ThrowsAny<Exception>(() => SvgFile.Parse("<html></html>"));
}
