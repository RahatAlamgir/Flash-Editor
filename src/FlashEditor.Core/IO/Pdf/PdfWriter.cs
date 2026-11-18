using System.Globalization;
using System.Text;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Pdf;

/// <summary>
/// Writes a PDF with one page per picture. Each page is exactly as big as its picture (at the chosen resolution) and holds the picture as a
/// JPEG, so the file stays small. No libraries are needed: the file is written by hand (PDF 1.4).
/// </summary>
public static class PdfWriter
{
    /// <param name="pages">One picture per page.</param>
    /// <param name="quality">JPEG quality 1..100.</param>
    /// <param name="dpi">How many pixels make one inch on paper: 96 keeps the on-screen size, 300 makes pages smaller on paper.</param>
    /// <param name="matte">Color behind transparent parts (paper color).</param>
    public static byte[] Build(IReadOnlyList<PixelBuffer> pages, int quality = 90, double dpi = 96, ColorRgba? matte = null)
    {
        if (pages.Count == 0) throw new ArgumentException("A PDF needs at least one page.", nameof(pages));
        dpi = Math.Clamp(dpi, 36, 1200);

        using var output = new MemoryStream();
        var offsets = new List<long>();      // byte position of object 1, 2, 3...
        void Text(string s) { var b = Encoding.ASCII.GetBytes(s); output.Write(b, 0, b.Length); }
        void BeginObject() { offsets.Add(output.Position); Text($"{offsets.Count} 0 obj\n"); }
        static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        Text("%PDF-1.4\n%âãÏÓ\n");

        // Object numbers: 1 = catalog, 2 = page tree, then three objects per page (page, contents, picture).
        int PageObject(int i) => 3 + i * 3;

        BeginObject();
        Text("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        BeginObject();
        Text("<< /Type /Pages /Count " + pages.Count + " /Kids [" + string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => PageObject(i) + " 0 R")) + "] >>\nendobj\n");

        for (int i = 0; i < pages.Count; i++)
        {
            var picture = pages[i];
            double w = picture.Width * 72.0 / dpi, h = picture.Height * 72.0 / dpi;
            byte[] jpeg = ImageEncoder.Encode(picture, ImageFileFormat.Jpeg, quality, matte ?? ColorRgba.White);
            int page = PageObject(i), contents = page + 1, image = page + 2;

            BeginObject();
            Text($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(w)} {N(h)}] /Resources << /XObject << /Im0 {image} 0 R >> >> /Contents {contents} 0 R >>\nendobj\n");

            string draw = $"q {N(w)} 0 0 {N(h)} 0 0 cm /Im0 Do Q";
            BeginObject();
            Text($"<< /Length {draw.Length} >>\nstream\n{draw}\nendstream\nendobj\n");

            BeginObject();
            Text($"<< /Type /XObject /Subtype /Image /Width {picture.Width} /Height {picture.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
            output.Write(jpeg, 0, jpeg.Length);
            Text("\nendstream\nendobj\n");
        }

        long xref = output.Position;
        Text($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets) Text(offset.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        Text($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    public static void Save(IReadOnlyList<PixelBuffer> pages, string path, int quality = 90, double dpi = 96, ColorRgba? matte = null)
    {
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, Build(pages, quality, dpi, matte));
        File.Move(temp, path, overwrite: true);
    }
}
