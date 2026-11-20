using System.IO.Compression;
using System.Xml;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Pptx;

namespace FlashEditor.Core.Tests.IO;

public class PptxWriterTests
{
    private static PixelBuffer Page(int w, int h, ColorRgba color)
    {
        var b = new PixelBuffer(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) b.SetPixel(x, y, color);
        return b;
    }

    [Fact]
    public void Writes_one_slide_and_picture_per_page_and_every_part_is_valid_xml()
    {
        var bytes = PptxWriter.Build(new[] { Page(160, 90, new ColorRgba(200, 30, 30)), Page(160, 90, new ColorRgba(30, 30, 200)) });
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var names = zip.Entries.Select(e => e.FullName).ToHashSet();
        foreach (var need in new[] { "[Content_Types].xml", "_rels/.rels", "ppt/presentation.xml", "ppt/slides/slide1.xml", "ppt/slides/slide2.xml", "ppt/media/image1.jpg", "ppt/media/image2.jpg", "ppt/theme/theme1.xml" })
            Assert.Contains(need, names);
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels")))
        {
            using var s = entry.Open();
            new XmlDocument().Load(s); // throws if not well-formed
        }
    }

    [Fact]
    public void Slide_has_the_size_of_the_page()
    {
        var bytes = PptxWriter.Build(new[] { Page(1280, 720, ColorRgba.White) });
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("ppt/presentation.xml")!.Open());
        Assert.Contains("cx=\"12192000\" cy=\"6858000\"", reader.ReadToEnd());
    }

    [Fact]
    public void Lossless_stores_png()
    {
        var bytes = PptxWriter.Build(new[] { Page(20, 20, ColorRgba.White) }, lossless: true);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(zip.GetEntry("ppt/media/image1.png"));
    }

    [Fact]
    public void No_pages_is_refused() => Assert.Throws<ArgumentException>(() => PptxWriter.Build(Array.Empty<PixelBuffer>()));
}
