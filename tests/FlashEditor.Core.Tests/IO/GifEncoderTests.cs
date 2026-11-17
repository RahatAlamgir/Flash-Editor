using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;

namespace FlashEditor.Core.Tests.IO;

/// <summary>The GIF writer, checked by reading its files back with the general decoder.</summary>
public class GifEncoderTests
{
    private static PixelBuffer Decode(byte[] gif) => ImageDecoder.Load(new MemoryStream(gif), ".gif");

    private static PixelBuffer Noise(int w, int h, int seed, int distinctColors)
    {
        var rnd = new Random(seed);
        var palette = Enumerable.Range(0, distinctColors).Select(_ => new ColorRgba((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256))).ToArray();
        var image = new PixelBuffer(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) image.SetPixel(x, y, palette[rnd.Next(distinctColors)]);
        return image;
    }

    [Fact]
    public void The_file_has_the_gif_signature_size_and_ending()
    {
        var bytes = GifEncoder.Encode(Noise(30, 20, 1, 5));
        Assert.Equal("GIF89a", System.Text.Encoding.ASCII.GetString(bytes, 0, 6));
        Assert.Equal(30, BitConverter.ToUInt16(bytes, 6));
        Assert.Equal(20, BitConverter.ToUInt16(bytes, 8));
        Assert.Equal(0x3B, bytes[^1]);
    }

    [Theory]
    [InlineData(1, 1, 1)]          // a single pixel
    [InlineData(2, 3, 2)]          // two colors: the smallest color table
    [InlineData(17, 9, 4)]
    [InlineData(40, 40, 17)]
    [InlineData(64, 48, 200)]
    [InlineData(100, 80, 256)]     // exactly 256 colors: stored exactly
    public void A_picture_with_up_to_256_colors_is_stored_exactly(int w, int h, int colors)
    {
        var image = Noise(w, h, seed: w + h + colors, colors);
        var back = Decode(GifEncoder.Encode(image));

        Assert.Equal((w, h), (back.Width, back.Height));
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) Assert.Equal(image.GetPixel(x, y), back.GetPixel(x, y));
    }

    [Fact]
    public void Big_noisy_pictures_survive_the_compression_table_filling_up_and_restarting()
    {
        // 256 random colors on 300 x 300 pixels: almost nothing repeats, so the LZW table fills many times over.
        var image = Noise(300, 300, 5, 256);
        var back = Decode(GifEncoder.Encode(image));
        Assert.Equal((300, 300), (back.Width, back.Height));
        for (int y = 0; y < 300; y += 7)
            for (int x = 0; x < 300; x += 3) Assert.Equal(image.GetPixel(x, y), back.GetPixel(x, y));
    }

    [Fact]
    public void Flat_pictures_compress_to_almost_nothing()
    {
        var image = new PixelBuffer(400, 300);
        image.Fill(new ColorRgba(10, 120, 200));
        var bytes = GifEncoder.Encode(image);
        Assert.True(bytes.Length < 1500, $"{bytes.Length} bytes");
        Assert.Equal(new ColorRgba(10, 120, 200), Decode(bytes).GetPixel(399, 299));
    }

    [Fact]
    public void Transparency_is_all_or_nothing_and_clear_areas_stay_clear()
    {
        var image = new PixelBuffer(6, 2);
        for (int x = 0; x < 6; x++)
        {
            byte alpha = x < 2 ? (byte)0 : x < 4 ? (byte)100 : (byte)255;          // clear, mostly clear, solid
            image.SetPixel(x, 0, new ColorRgba(200, 30, 30, alpha));
            image.SetPixel(x, 1, new ColorRgba(30, 30, 200, alpha));
        }
        var back = Decode(GifEncoder.Encode(image));

        Assert.Equal(0, back.GetPixel(0, 0).A);
        Assert.Equal(0, back.GetPixel(1, 1).A);
        Assert.Equal(0, back.GetPixel(2, 0).A);                                      // under half opaque: clear
        Assert.Equal(255, back.GetPixel(4, 0).A);
        Assert.Equal(new ColorRgba(200, 30, 30), back.GetPixel(5, 0));
        Assert.Equal(new ColorRgba(30, 30, 200), back.GetPixel(4, 1));
    }

    [Fact]
    public void A_picture_with_many_colors_is_reduced_to_a_close_match()
    {
        var gradient = new PixelBuffer(256, 64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 256; x++) gradient.SetPixel(x, y, new ColorRgba((byte)x, (byte)(y * 4), (byte)(255 - x)));   // 16k different colors

        var back = Decode(GifEncoder.Encode(gradient));
        long error = 0;
        int worst = 0;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 256; x++)
            {
                var a = gradient.GetPixel(x, y); var b = back.GetPixel(x, y);
                int d = Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
                error += d; worst = Math.Max(worst, d);
            }
        double average = error / (256.0 * 64);
        Assert.True(average < 14, $"average error per pixel {average:F1}");
        Assert.True(worst < 70, $"worst error {worst}");
    }

    [Fact]
    public void Dithering_can_be_switched_off()
    {
        // 512 different colors that only depend on the column: more than a GIF can hold, and every row is the same.
        var picture = new PixelBuffer(512, 4);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 512; x++) picture.SetPixel(x, y, new ColorRgba((byte)(x / 2), (byte)(x * 7 % 256), (byte)(255 - x / 2)));

        bool RowsEqual(PixelBuffer p) { for (int x = 0; x < 512; x++) if (!p.GetPixel(x, 0).Equals(p.GetPixel(x, 1))) return false; return true; }

        Assert.True(RowsEqual(Decode(GifEncoder.Encode(picture, dither: false))));   // no error is passed down: rows stay identical
        Assert.False(RowsEqual(Decode(GifEncoder.Encode(picture, dither: true))));   // dithering carries the error to the next row
    }

    [Fact]
    public void Saving_gif_works_through_the_normal_save_methods()
    {
        string path = Path.Combine(Path.GetTempPath(), $"flash-test-{Guid.NewGuid():N}.gif");
        try
        {
            ImageEncoder.Save(Noise(20, 20, 3, 6), path, ImageFileFormat.Gif);
            Assert.Equal((20, 20), (ImageDecoder.Load(path).Width, ImageDecoder.Load(path).Height));
        }
        finally { File.Delete(path); }
    }
}
