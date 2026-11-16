using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.Tests.IO;

public class ExportTests
{
    private static PixelBuffer Photo(int w, int h)
    {
        // Smooth gradient plus detail, like a photo.
        var image = new PixelBuffer(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                image.SetPixel(x, y, new ColorRgba((byte)(x * 255 / w), (byte)(y * 255 / h), (byte)((x * y) % 256)));
        return image;
    }

    [Fact]
    public void Export_resizes_to_the_chosen_size()
    {
        var bytes = ImageExporter.Encode(Photo(200, 100), new ExportSettings(ImageFileFormat.Png, 100, 50, 25));
        using var stream = new MemoryStream(bytes);
        var decoded = ImageDecoder.Load(stream);
        Assert.Equal((50, 25), (decoded.Width, decoded.Height));
    }

    [Fact]
    public void Lower_jpeg_quality_gives_a_smaller_file()
    {
        var image = Photo(256, 256);
        long high = ImageExporter.Encode(image, new ExportSettings(ImageFileFormat.Jpeg, 95, 256, 256)).LongLength;
        long low = ImageExporter.Encode(image, new ExportSettings(ImageFileFormat.Jpeg, 30, 256, 256)).LongLength;
        Assert.True(low < high, $"{low} should be < {high}");
    }

    [Fact]
    public void Export_does_not_change_the_source_image()
    {
        var image = Photo(64, 64);
        var copy = image.Clone();
        ImageExporter.Encode(image, new ExportSettings(ImageFileFormat.Webp, 80, 32, 32));
        Assert.True(copy.ContentEquals(image));
    }

    [Theory]
    [InlineData(ResampleMode.Smooth)]
    [InlineData(ResampleMode.Bicubic)]
    [InlineData(ResampleMode.Lanczos)]
    [InlineData(ResampleMode.NearestNeighbor)]
    public void Every_resize_method_keeps_a_solid_color(ResampleMode mode)
    {
        var color = new ColorRgba(40, 150, 220);
        foreach (var (w, h) in new[] { (37, 23), (300, 180) }) // shrink and enlarge
        {
            var result = Resampler.Resize(TestImages.Solid(100, 60, color), w, h, mode);
            Assert.Equal((w, h), (result.Width, result.Height));
            Assert.Equal(color, result.GetPixel(w / 2, h / 2));
            Assert.Equal(color, result.GetPixel(0, 0));
        }
    }

    [Fact]
    public void File_sizes_are_shown_in_readable_units()
    {
        Assert.Equal("512 bytes", ImageExporter.FormatFileSize(512));
        Assert.Equal("2 KB", ImageExporter.FormatFileSize(2048));
        Assert.Equal("1.5 MB", ImageExporter.FormatFileSize(1024 * 1024 * 3 / 2));
    }
}
