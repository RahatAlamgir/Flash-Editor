using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Barcodes;
using FlashEditor.Core.Qr;

namespace FlashEditor.Core.Tests.Objects;

public class GeneratedObjectsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "flash-gen-tests-" + Guid.NewGuid().ToString("N"));
    public GeneratedObjectsTests() => Directory.CreateDirectory(_folder);
    public void Dispose() { try { Directory.Delete(_folder, true); } catch { /* temp */ } }

    [Fact]
    public void Chart_numbers_are_read_from_lines_with_several_separators()
    {
        var items = ChartData.Parse("Jan, 12\nFeb; 19.5\nMar:8\n\nbad line\nApr\t15\n  7  \nWith, comma, 3");
        Assert.Equal(new[] { "Jan", "Feb", "Mar", "Apr", "5", "With, comma" }, items.Select(i => i.Label));
        Assert.Equal(new[] { 12.0, 19.5, 8, 15, 7, 3 }, items.Select(i => i.Value));
    }

    [Fact]
    public void Chart_numbers_can_be_written_and_read_again()
    {
        var items = new[] { new ChartItem("A", 1.5), new ChartItem("B", -2) };
        Assert.Equal(items, ChartData.Parse(ChartData.Format(items)));
    }

    [Fact]
    public void A_chart_uses_its_colors_in_turn_and_falls_back_to_a_palette()
    {
        var chart = ChartObject.Sample(new ObjectBox(0, 0, 10, 10)) with { Colors = new[] { new ColorRgba(1, 2, 3), new ColorRgba(4, 5, 6) } };
        Assert.Equal(new ColorRgba(1, 2, 3), chart.ColorOf(0));
        Assert.Equal(new ColorRgba(1, 2, 3), chart.ColorOf(2));
        Assert.Equal(ChartPalettes.Ocean[0], (chart with { Colors = Array.Empty<ColorRgba>() }).ColorOf(0));
    }

    [Fact]
    public void Barcodes_and_bent_lines_survive_saving_a_project()
    {
        var code = new BarcodeObject(new ObjectBox(2, 2, 60, 24), "590123412345", BarcodeKind.Ean13, new ColorRgba(1, 2, 3), new ColorRgba(250, 250, 250), false, true);
        var line = new ShapeObject(new ObjectBox(5, 40, 60, 6), ShapeKind.Line, null, new ColorRgba(9, 9, 9), 4, ArrowEnd: true, Bend: -0.4, BendStyle: LineBendStyle.Wave);
        var doc = new Document(80, 60);
        var one = new Layer("Bar", 80, 60); one.RestoreState(new LayerState(new PixelBuffer(80, 60), null, 0, 0, code));
        var two = new Layer("Line", 80, 60); two.RestoreState(new LayerState(new PixelBuffer(80, 60), null, 0, 0, line));
        doc.SetLayers(new List<Layer> { new Layer("Background", new PixelBuffer(80, 60)), one, two }, null);
        string path = Path.Combine(_folder, "bar.flash");
        ProjectWriter.Save(doc, path);

        var back = ProjectReader.Open(path);
        var b = Assert.IsType<BarcodeObject>(back.Layers[1].Object);
        Assert.Equal(("590123412345", BarcodeKind.Ean13, false, true), (b.Content, b.Kind, b.ShowText, b.TransparentBackground));
        var l = Assert.IsType<ShapeObject>(back.Layers[2].Object);
        Assert.Equal((-0.4, LineBendStyle.Wave, true), (l.Bend, l.BendStyle, l.ArrowEnd));
    }

    [Fact]
    public void A_picture_in_a_frame_covers_it_and_survives_saving()
    {
        var wide = new PixelBuffer(40, 20);
        wide.SetPixel(3, 3, new ColorRgba(10, 20, 30));
        var picture = new PictureFill(wide, "Sky", Zoom: 2, PanX: 0.5, PanY: -1);

        // A 40 x 20 picture in a 30 x 30 frame: scaled to cover (x1.5 = 60 x 30), then zoomed by 2 (120 x 60).
        var (x, y, w, h) = picture.Place(30, 30);
        Assert.Equal((120.0, 60.0), (w, h));
        Assert.Equal(-22.5, x, 6);   // centered (-45), a quarter of the way to its right edge
        Assert.Equal(-30.0, y, 6);   // the top edge of the picture sits against the top of the frame

        var frame = new ShapeObject(new ObjectBox(2, 2, 30, 30), ShapeKind.Ellipse, new ColorRgba(200, 200, 200), null, 0, Picture: picture);
        var doc = new Document(60, 40);
        var layer = new Layer("Frame", 60, 40); layer.RestoreState(new LayerState(new PixelBuffer(60, 40), null, 0, 0, frame));
        doc.SetLayers(new List<Layer> { new Layer("Background", new PixelBuffer(60, 40)), layer }, null);
        string path = Path.Combine(_folder, "framed.flash");
        ProjectWriter.Save(doc, path);

        var back = Assert.IsType<ShapeObject>(ProjectReader.Open(path).Layers[1].Object);
        Assert.NotNull(back.Picture);
        Assert.Equal(("Sky", 2.0, 0.5, -1.0), (back.Picture!.Name, back.Picture.Zoom, back.Picture.PanX, back.Picture.PanY));
        Assert.Equal(new ColorRgba(10, 20, 30), back.Picture.Image.GetPixel(3, 3));
    }

    [Fact]
    public void QR_codes_and_charts_survive_saving_a_project()
    {
        var qr = new QrCodeObject(new ObjectBox(2, 2, 30, 30), "https://x.test", new ColorRgba(1, 2, 3), new ColorRgba(250, 250, 250), QrLevel.High, QrStyle.Dots, 3, true);
        var chart = new ChartObject(new ObjectBox(5, 5, 40, 30), ChartKind.Donut, new[] { new ChartItem("a", 1), new ChartItem("b", 2) }, ChartPalettes.Candy,
            "Title", ShowValues: false, Background: new ColorRgba(9, 9, 9), TextColor: new ColorRgba(7, 7, 7));
        var doc = new Document(80, 60);
        var one = new Layer("QR", 80, 60); one.RestoreState(new LayerState(new PixelBuffer(80, 60), null, 0, 0, qr));
        var two = new Layer("Chart", 80, 60); two.RestoreState(new LayerState(new PixelBuffer(80, 60), null, 0, 0, chart));
        doc.SetLayers(new List<Layer> { new Layer("Background", new PixelBuffer(80, 60)), one, two }, null);
        string path = Path.Combine(_folder, "gen.flash");
        ProjectWriter.Save(doc, path);

        var back = ProjectReader.Open(path);
        var q = Assert.IsType<QrCodeObject>(back.Layers[1].Object);
        Assert.Equal(("https://x.test", QrLevel.High, QrStyle.Dots, 3, true), (q.Content, q.Level, q.Style, q.Quiet, q.TransparentBackground));
        var c = Assert.IsType<ChartObject>(back.Layers[2].Object);
        Assert.Equal((ChartKind.Donut, "Title", false), (c.Kind, c.Title, c.ShowValues));
        Assert.Equal(new[] { "a", "b" }, c.Items.Select(i => i.Label));
        Assert.Equal(ChartPalettes.Candy.Count, c.Colors.Count);
        Assert.Equal(new ColorRgba(9, 9, 9), c.Background);
    }
}
