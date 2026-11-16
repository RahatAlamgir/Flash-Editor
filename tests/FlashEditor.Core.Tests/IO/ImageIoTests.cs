using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;

namespace FlashEditor.Core.Tests.IO;

public class ImageIoTests
{
    private static PixelBuffer RoundTrip(PixelBuffer image, ImageFileFormat format, int quality = 100)
    {
        var bytes = ImageEncoder.Encode(image, format, quality);
        using var stream = new MemoryStream(bytes);
        return ImageDecoder.Load(stream);
    }

    [Fact]
    public void Png_round_trip_is_lossless_including_transparency()
    {
        var image = TestImages.Numbered(16, 16);
        image.SetPixel(3, 3, new ColorRgba(10, 20, 30, 128));
        var decoded = RoundTrip(image, ImageFileFormat.Png);
        Assert.True(image.ContentEquals(decoded));
    }

    [Fact]
    public void Webp_lossless_round_trip()
    {
        var image = TestImages.Numbered(16, 16);
        var decoded = RoundTrip(image, ImageFileFormat.Webp, 100);
        Assert.Equal((16, 16), (decoded.Width, decoded.Height));
        Assert.Equal(image.GetPixel(5, 7), decoded.GetPixel(5, 7));
    }

    [Fact]
    public void Jpeg_has_no_transparency()
    {
        var image = new PixelBuffer(8, 8); // fully transparent
        var decoded = RoundTrip(image, ImageFileFormat.Jpeg, 90);
        var p = decoded.GetPixel(4, 4);
        Assert.Equal(255, p.A);
        Assert.True(p.R > 240); // transparent became white
    }

    [Fact]
    public void Ico_contains_all_sizes_and_can_be_read_back()
    {
        var image = TestImages.Solid(300, 200, new ColorRgba(0, 120, 255));
        var bytes = IcoEncoder.Encode(image);

        Assert.Equal(0, BitConverter.ToUInt16(bytes, 0));        // reserved
        Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));        // type = icon
        Assert.Equal(IcoEncoder.DefaultSizes.Length, BitConverter.ToUInt16(bytes, 4));

        using var stream = new MemoryStream(bytes);
        var decoded = ImageDecoder.Load(stream);
        Assert.Equal(decoded.Width, decoded.Height); // square icon
    }

    [Fact]
    public void Formats_are_detected_from_extension()
    {
        Assert.Equal(ImageFileFormat.Jpeg, ImageFileFormats.FromPath("photo.JPEG"));
        Assert.Equal(ImageFileFormat.Webp, ImageFileFormats.FromPath(@"c:\x\a.webp"));
        Assert.Equal(ImageFileFormat.Ico, ImageFileFormats.FromPath("app.ico"));
        Assert.Null(ImageFileFormats.FromPath("notes.txt"));
    }

    [Fact]
    public void Invalid_file_gives_a_clear_error()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(stream));
    }
}
