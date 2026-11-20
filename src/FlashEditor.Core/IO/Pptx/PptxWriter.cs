using System.IO.Compression;
using System.Text;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Pptx;

/// <summary>
/// Writes a PowerPoint file (.pptx) with one slide per picture. Each slide is exactly as big as the page and holds the page as one
/// picture (JPEG or PNG), so it looks the same everywhere. No libraries are needed: a .pptx is a zip of small XML files.
/// </summary>
public static class PptxWriter
{
    private const double EmuPerPixel = 9525;        // 96 dpi
    private const long MaxSlideEmu = 51_206_400;    // the biggest slide PowerPoint accepts (56 in)

    /// <param name="pages">One picture per slide. All slides get the size of the first page.</param>
    /// <param name="quality">JPEG quality 1..100 (ignored when <paramref name="lossless"/> is true).</param>
    /// <param name="lossless">Store pictures as PNG (sharper, bigger file) instead of JPEG.</param>
    /// <param name="matte">Color behind transparent parts.</param>
    public static byte[] Build(IReadOnlyList<PixelBuffer> pages, int quality = 90, bool lossless = false, ColorRgba? matte = null)
    {
        if (pages.Count == 0) throw new ArgumentException("A presentation needs at least one slide.", nameof(pages));
        var slides = new List<PptxSlide>();
        for (int i = 0; i < pages.Count; i++)
        {
            var slide = new PptxSlide();
            byte[] data = lossless
                ? ImageEncoder.Encode(pages[i], ImageFileFormat.Png, 100, matte)
                : ImageEncoder.Encode(pages[i], ImageFileFormat.Jpeg, quality, matte ?? ColorRgba.White);
            slide.AddPicture(data, lossless, $"Page {i + 1}", 0, 0, pages[0].Width, pages[0].Height);
            slides.Add(slide);
        }
        return Build(slides, pages[0].Width, pages[0].Height);
    }

    /// <summary>A presentation made of ready slides (see <see cref="PptxSlide"/>) on a page of the given size in pixels.</summary>
    public static byte[] Build(IReadOnlyList<PptxSlide> slides, int pageWidth, int pageHeight)
    {
        if (slides.Count == 0) throw new ArgumentException("A presentation needs at least one slide.", nameof(slides));
        double scale = Math.Min(1.0, MaxSlideEmu / (Math.Max(pageWidth, pageHeight) * EmuPerPixel));
        long slideW = (long)Math.Round(pageWidth * EmuPerPixel * scale), slideH = (long)Math.Round(pageHeight * EmuPerPixel * scale);

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text) => AddBytes(name, new UTF8Encoding(false).GetBytes(text));
            void AddBytes(string name, byte[] data)
            {
                var entry = zip.CreateEntry(name, name.StartsWith("ppt/media/") ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(data, 0, data.Length);
            }

            Add("[Content_Types].xml", ContentTypes(slides.Count));
            Add("_rels/.rels", Rels(("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "ppt/presentation.xml")));
            Add("ppt/presentation.xml", Presentation(slides.Count, slideW, slideH));
            var presentationRels = new List<(string, string, string)>
            {
                ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster", "slideMasters/slideMaster1.xml"),
                ("rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme", "theme/theme1.xml"),
            };
            for (int i = 0; i < slides.Count; i++)
                presentationRels.Add(($"rId{i + 3}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide", $"slides/slide{i + 1}.xml"));
            Add("ppt/_rels/presentation.xml.rels", Rels(presentationRels.ToArray()));
            Add("ppt/theme/theme1.xml", Theme);
            Add("ppt/slideMasters/slideMaster1.xml", SlideMaster);
            Add("ppt/slideMasters/_rels/slideMaster1.xml.rels", Rels(
                ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout", "../slideLayouts/slideLayout1.xml"),
                ("rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme", "../theme/theme1.xml")));
            Add("ppt/slideLayouts/slideLayout1.xml", SlideLayout);
            Add("ppt/slideLayouts/_rels/slideLayout1.xml.rels", Rels(
                ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster", "../slideMasters/slideMaster1.xml")));

            int mediaNumber = 0;
            for (int i = 0; i < slides.Count; i++)
            {
                var rels = new List<(string, string, string)> { ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout", "../slideLayouts/slideLayout1.xml") };
                for (int m = 0; m < slides[i].Media.Count; m++)
                {
                    var media = slides[i].Media[m];
                    string file = $"image{++mediaNumber}.{(media.Png ? "png" : "jpg")}";
                    AddBytes("ppt/media/" + file, media.Data);
                    rels.Add(($"rId{m + 2}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "../media/" + file));
                }
                Add($"ppt/slides/slide{i + 1}.xml", Slide(slides[i], scale));
                Add($"ppt/slides/_rels/slide{i + 1}.xml.rels", Rels(rels.ToArray()));
            }
        }
        return stream.ToArray();
    }

    public static void Save(IReadOnlyList<PixelBuffer> pages, string path, int quality = 90, bool lossless = false, ColorRgba? matte = null)
    {
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, Build(pages, quality, lossless, matte));
        File.Move(temp, path, overwrite: true);
    }

    // ------------------------------------------------------------------ the XML parts

    private const string Decl = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";
    private const string NsA = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string NsR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string NsP = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private static string Rels(params (string Id, string Type, string Target)[] items)
        => Decl + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
           + string.Concat(items.Select(r => $"<Relationship Id=\"{r.Id}\" Type=\"{r.Type}\" Target=\"{r.Target}\"/>")) + "</Relationships>";

    private static string ContentTypes(int slides)
        => Decl + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
           + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
           + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
           + "<Default Extension=\"jpg\" ContentType=\"image/jpeg\"/>"
           + "<Default Extension=\"png\" ContentType=\"image/png\"/>"
           + "<Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>"
           + "<Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/>"
           + "<Override PartName=\"/ppt/slideLayouts/slideLayout1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/>"
           + "<Override PartName=\"/ppt/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/>"
           + string.Concat(Enumerable.Range(1, slides).Select(i => $"<Override PartName=\"/ppt/slides/slide{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>"))
           + "</Types>";

    private static string Presentation(int slides, long w, long h)
        => Decl + $"<p:presentation xmlns:a=\"{NsA}\" xmlns:r=\"{NsR}\" xmlns:p=\"{NsP}\">"
           + "<p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst>"
           + "<p:sldIdLst>" + string.Concat(Enumerable.Range(1, slides).Select(i => $"<p:sldId id=\"{255 + i}\" r:id=\"rId{i + 2}\"/>")) + "</p:sldIdLst>"
           + $"<p:sldSz cx=\"{w}\" cy=\"{h}\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/></p:presentation>";

    private static string Slide(PptxSlide slide, double scale)
        => Decl + $"<p:sld xmlns:a=\"{NsA}\" xmlns:r=\"{NsR}\" xmlns:p=\"{NsP}\"><p:cSld><p:spTree>"
           + "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>"
           + "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>"
           + slide.ToXml(scale)
           + "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>";

    private static readonly string SlideLayout = Decl + $"<p:sldLayout xmlns:a=\"{NsA}\" xmlns:r=\"{NsR}\" xmlns:p=\"{NsP}\" type=\"blank\" preserve=\"1\"><p:cSld name=\"Blank\"><p:spTree>"
        + "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>"
        + "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>"
        + "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>";

    private static readonly string SlideMaster = Decl + $"<p:sldMaster xmlns:a=\"{NsA}\" xmlns:r=\"{NsR}\" xmlns:p=\"{NsP}\"><p:cSld>"
        + "<p:bg><p:bgRef idx=\"1001\"><a:schemeClr val=\"bg1\"/></p:bgRef></p:bg><p:spTree>"
        + "<p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>"
        + "<p:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"0\" cy=\"0\"/></a:xfrm></p:grpSpPr>"
        + "</p:spTree></p:cSld>"
        + "<p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>"
        + "<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rId1\"/></p:sldLayoutIdLst>"
        + "<p:txStyles><p:titleStyle><a:lvl1pPr><a:defRPr sz=\"4400\"/></a:lvl1pPr></p:titleStyle><p:bodyStyle><a:lvl1pPr><a:defRPr sz=\"2800\"/></a:lvl1pPr></p:bodyStyle><p:otherStyle><a:lvl1pPr><a:defRPr sz=\"1800\"/></a:lvl1pPr></p:otherStyle></p:txStyles>"
        + "</p:sldMaster>";

    private static readonly string Theme = Decl + $"<a:theme xmlns:a=\"{NsA}\" name=\"Flash Editor\"><a:themeElements>"
        + "<a:clrScheme name=\"Flash Editor\"><a:dk1><a:srgbClr val=\"000000\"/></a:dk1><a:lt1><a:srgbClr val=\"FFFFFF\"/></a:lt1><a:dk2><a:srgbClr val=\"1F2937\"/></a:dk2><a:lt2><a:srgbClr val=\"F3F4F6\"/></a:lt2>"
        + "<a:accent1><a:srgbClr val=\"2563EB\"/></a:accent1><a:accent2><a:srgbClr val=\"F97316\"/></a:accent2><a:accent3><a:srgbClr val=\"14B8A6\"/></a:accent3><a:accent4><a:srgbClr val=\"EC4899\"/></a:accent4><a:accent5><a:srgbClr val=\"8B5CF6\"/></a:accent5><a:accent6><a:srgbClr val=\"F59E0B\"/></a:accent6>"
        + "<a:hlink><a:srgbClr val=\"2563EB\"/></a:hlink><a:folHlink><a:srgbClr val=\"7C3AED\"/></a:folHlink></a:clrScheme>"
        + "<a:fontScheme name=\"Flash Editor\"><a:majorFont><a:latin typeface=\"Segoe UI\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont><a:minorFont><a:latin typeface=\"Segoe UI\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>"
        + "<a:fmtScheme name=\"Flash Editor\">"
        + "<a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:fillStyleLst>"
        + "<a:lnStyleLst><a:ln w=\"6350\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln><a:ln w=\"19050\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln></a:lnStyleLst>"
        + "<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>"
        + "<a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:bgFillStyleLst>"
        + "</a:fmtScheme></a:themeElements></a:theme>";
}
