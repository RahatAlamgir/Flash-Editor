using System.Text;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.IO.Pdf;
using Xunit;

namespace FlashEditor.Core.Tests.IO;

public class PdfAndGifAnimationTests
{
    private static PixelBuffer Solid(int w, int h, ColorRgba c)
    {
        var p = new PixelBuffer(w, h);
        p.Fill(c);
        return p;
    }

    [Fact]
    public void A_pdf_has_one_page_per_picture_at_the_right_size()
    {
        var bytes = PdfWriter.Build(new[] { Solid(96, 48, new ColorRgba(255, 0, 0)), Solid(192, 96, new ColorRgba(0, 255, 0)) }, dpi: 96);
        string text = Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("/Count 2", text);
        Assert.Contains("/MediaBox [0 0 72 36]", text);      // 96 px at 96 dpi = 72 points
        Assert.Contains("/MediaBox [0 0 144 72]", text);
        Assert.EndsWith("%%EOF\n", text);
    }

    [Fact]
    public void The_cross_reference_table_points_at_the_objects()
    {
        var bytes = PdfWriter.Build(new[] { Solid(20, 20, new ColorRgba(1, 2, 3)) });
        string text = Encoding.Latin1.GetString(bytes);
        int startxref = int.Parse(text[(text.LastIndexOf("startxref\n") + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[startxref..]);
        var lines = text[startxref..].Split('\n');
        int count = int.Parse(lines[1].Split(' ')[1]);
        for (int i = 1; i < count; i++)
        {
            int offset = int.Parse(lines[2 + i].Split(' ')[0]);
            Assert.StartsWith($"{i} 0 obj", text[offset..]);
        }
    }

    [Fact]
    public void A_pdf_needs_a_page()
        => Assert.Throws<ArgumentException>(() => PdfWriter.Build(Array.Empty<PixelBuffer>()));

    [Fact]
    public void An_animated_gif_holds_every_frame_with_its_delay_and_loops()
    {
        var bytes = GifEncoder.EncodeAnimation(new[] { Solid(8, 8, new ColorRgba(255, 0, 0)), Solid(8, 8, new ColorRgba(0, 0, 255)), Solid(8, 8, new ColorRgba(0, 255, 0)) }, delayMs: 500);
        Assert.Equal("GIF89a", Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.Contains("NETSCAPE2.0", Encoding.ASCII.GetString(bytes));
        int images = 0;
        for (int i = 0; i < bytes.Length - 1; i++)
            if (bytes[i] == 0x21 && bytes[i + 1] == 0xF9 && bytes[i + 2] == 4) { images++; Assert.Equal(50, bytes[i + 4] | bytes[i + 5] << 8); }
        Assert.Equal(3, images);
        Assert.Equal(0x3B, bytes[^1]);
    }

    [Fact]
    public void A_gif_that_plays_once_has_no_loop_block()
    {
        var bytes = GifEncoder.EncodeAnimation(new[] { Solid(4, 4, ColorRgba.White) }, loop: false);
        Assert.DoesNotContain("NETSCAPE2.0", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void Frames_of_another_size_are_scaled_to_the_first()
    {
        var bytes = GifEncoder.EncodeAnimation(new[] { Solid(10, 6, ColorRgba.White), Solid(20, 12, ColorRgba.Black) });
        Assert.Equal(10, bytes[6] | bytes[7] << 8);
        Assert.Equal(6, bytes[8] | bytes[9] << 8);
    }
}
