using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;

namespace FlashEditor.Core.Tests.IO;

/// <summary>More file types: names and filters, the BMP writer, and how formats the built-in codec lacks are handed on.</summary>
public class MoreFormatsTests
{
    private static PixelBuffer Sample()
    {
        var image = new PixelBuffer(7, 5);
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 7; x++)
                image.SetPixel(x, y, new ColorRgba((byte)(x * 35), (byte)(y * 50), (byte)(x * y * 7), (byte)(x == 0 ? 0 : 50 + x * 30 + y)));
        image.SetPixel(3, 2, new ColorRgba(255, 128, 0, 255));
        return image;
    }

    [Fact]
    public void Every_format_has_a_matching_extension_and_dialog_position()
    {
        foreach (var format in Enum.GetValues<ImageFileFormat>())
        {
            string extension = ImageFileFormats.Extension(format);
            Assert.Equal(format, ImageFileFormats.FromPath("picture" + extension));
            Assert.Equal(format, ImageFileFormats.FromFilterIndex(ImageFileFormats.ToFilterIndex(format)));
        }
        Assert.Equal(ImageFileFormat.Tiff, ImageFileFormats.FromPath("scan.TIFF"));
        Assert.Equal(7, ImageFileFormats.SaveFilter.Split('|').Length / 2);   // one "name|pattern" pair per format
    }

    [Theory]
    [InlineData("a.png"), InlineData("a.JPG"), InlineData("a.bmp"), InlineData("a.gif"), InlineData("a.tif"), InlineData("a.TIFF")]
    [InlineData("a.heic"), InlineData("a.avif"), InlineData("a.jxr"), InlineData("a.dds"), InlineData("a.cr2"), InlineData("a.dng")]
    public void These_files_can_be_opened(string name) => Assert.True(ImageFileFormats.CanOpen(name));

    [Theory]
    [InlineData("a.txt"), InlineData("a.pdf"), InlineData("a"), InlineData("a.docx")]
    public void These_files_cannot(string name) => Assert.False(ImageFileFormats.CanOpen(name));

    [Fact]
    public void The_open_dialog_lists_every_openable_extension()
    {
        string all = ImageFileFormats.OpenFilter.Split('|')[1];
        foreach (var extension in ImageFileFormats.OpenExtensions) Assert.Contains("*" + extension, all);
    }

    [Fact]
    public void A_bmp_file_is_written_correctly_and_keeps_transparency()
    {
        var image = Sample();
        var bytes = BmpEncoder.Encode(image);

        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
        Assert.Equal(bytes.Length, BitConverter.ToInt32(bytes, 2));         // size in the header is the real size
        Assert.Equal(122, BitConverter.ToInt32(bytes, 10));                // pixel data starts after the headers
        Assert.Equal(image.Width, BitConverter.ToInt32(bytes, 18));
        Assert.Equal(image.Height, BitConverter.ToInt32(bytes, 22));

        // Read it back with the general decoder (SkiaSharp): same picture, same alpha.
        var decoded = ImageDecoder.Load(new MemoryStream(bytes), ".bmp");
        Assert.Equal(image.Width, decoded.Width);
        Assert.Equal(image.Height, decoded.Height);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                var expected = image.GetPixel(x, y);
                var actual = decoded.GetPixel(x, y);
                Assert.Equal(expected.A, actual.A);
                if (expected.A > 0) Assert.Equal(expected, actual);       // colors of fully transparent pixels do not matter
            }
    }

    [Fact]
    public void Saving_bmp_works_through_the_normal_save_methods()
    {
        string path = Path.Combine(Path.GetTempPath(), $"flash-test-{Guid.NewGuid():N}.bmp");
        try
        {
            ImageEncoder.Save(Sample(), path, ImageFileFormat.Bmp);
            var loaded = ImageDecoder.Load(path);
            Assert.Equal(7, loaded.Width);
            Assert.Equal(new ColorRgba(255, 128, 0, 255), loaded.GetPixel(3, 2));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Tiff_needs_the_fallback_encoder_and_uses_it_when_it_is_set()
    {
        var saved = ImageEncoder.FallbackEncoder;
        try
        {
            ImageEncoder.FallbackEncoder = null;
            Assert.Throws<NotSupportedException>(() => ImageEncoder.Encode(Sample(), ImageFileFormat.Tiff));

            ImageFileFormat? asked = null;
            ImageEncoder.FallbackEncoder = (_, format) => { asked = format; return new byte[] { 1, 2, 3 }; };
            Assert.Equal(new byte[] { 1, 2, 3 }, ImageEncoder.Encode(Sample(), ImageFileFormat.Tiff));
            Assert.Equal(ImageFileFormat.Tiff, asked);

            // GIF does not need the fallback: the engine writes it itself
            ImageEncoder.FallbackEncoder = null;
            Assert.Equal((byte)'G', ImageEncoder.Encode(Sample(), ImageFileFormat.Gif)[0]);
        }
        finally { ImageEncoder.FallbackEncoder = saved; }
    }

    [Fact]
    public void Files_the_built_in_decoder_does_not_know_go_to_the_fallback_decoder()
    {
        var saved = ImageDecoder.FallbackDecoder;
        try
        {
            byte[] notAPictureForSkia = { (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 0, 0 };   // looks like the start of a TIFF
            var expected = Sample();

            byte[]? received = null;
            ImageDecoder.FallbackDecoder = bytes => { received = bytes; return expected; };
            var result = ImageDecoder.Load(new MemoryStream(notAPictureForSkia), ".tif");
            Assert.Same(expected, result);
            Assert.Equal(notAPictureForSkia, received);
        }
        finally { ImageDecoder.FallbackDecoder = saved; }
    }

    [Fact]
    public void When_nothing_can_read_the_file_the_message_says_what_to_do()
    {
        var saved = ImageDecoder.FallbackDecoder;
        try
        {
            byte[] junk = { 1, 2, 3, 4, 5, 6, 7, 8 };

            ImageDecoder.FallbackDecoder = null;
            var plain = Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(new MemoryStream(junk), ".xyz"));
            Assert.Contains("not a supported image", plain.Message);

            ImageDecoder.FallbackDecoder = _ => throw new InvalidOperationException("no imaging component");   // a codec Windows lacks
            var heic = Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(new MemoryStream(junk), ".heic"));
            Assert.Contains("HEIF Image Extensions", heic.Message);
            var avif = Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(new MemoryStream(junk), ".avif"));
            Assert.Contains("AV1", avif.Message);
            var raw = Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(new MemoryStream(junk), ".nef"));
            Assert.Contains("Raw Image Extension", raw.Message);

            ImageDecoder.FallbackDecoder = _ => null;                                                           // decoder gave up
            Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(new MemoryStream(junk), ".tif"));
        }
        finally { ImageDecoder.FallbackDecoder = saved; }
    }

    [Fact]
    public void Exif_orientation_values_turn_the_picture_upright()
    {
        var image = new PixelBuffer(3, 2);
        image.SetPixel(0, 0, new ColorRgba(255, 0, 0));                                   // red marks the top-left corner
        Assert.Same(image, ImageDecoder.ApplyExifOrientation(image, 1));                  // already upright: untouched
        var rotated = ImageDecoder.ApplyExifOrientation(image, 6);                        // 6 = needs a quarter turn clockwise
        Assert.Equal((2, 3), (rotated.Width, rotated.Height));
        Assert.Equal(new ColorRgba(255, 0, 0), rotated.GetPixel(1, 0));                   // the red corner moved to the top-right
        var mirrored = ImageDecoder.ApplyExifOrientation(image, 2);
        Assert.Equal(new ColorRgba(255, 0, 0), mirrored.GetPixel(2, 0));
    }
}
