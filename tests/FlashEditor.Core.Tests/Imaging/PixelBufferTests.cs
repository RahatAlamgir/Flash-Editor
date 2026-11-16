using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Imaging;

public class PixelBufferTests
{
    [Fact]
    public void Fill_sets_every_pixel()
    {
        var buffer = TestImages.Solid(7, 5, new ColorRgba(10, 20, 30, 40));
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 7; x++)
                Assert.Equal(new ColorRgba(10, 20, 30, 40), buffer.GetPixel(x, y));
    }

    [Fact]
    public void Crop_outside_bounds_is_transparent()
    {
        var buffer = TestImages.Solid(4, 4, ColorRgba.White);
        var crop = buffer.Crop(new PixelRect(2, 2, 4, 4));

        Assert.Equal(ColorRgba.White, crop.GetPixel(0, 0));
        Assert.Equal(ColorRgba.White, crop.GetPixel(1, 1));
        Assert.Equal(ColorRgba.Transparent, crop.GetPixel(2, 2));
    }

    [Fact]
    public void CopyFrom_clips_negative_destination()
    {
        var source = TestImages.Numbered(4, 4);
        var target = new PixelBuffer(4, 4);
        target.CopyFrom(source, -1, -1);

        Assert.Equal(source.GetPixel(1, 1), target.GetPixel(0, 0));
        Assert.Equal(source.GetPixel(3, 3), target.GetPixel(2, 2));
        Assert.Equal(ColorRgba.Transparent, target.GetPixel(3, 3));
    }

    [Fact]
    public void Hex_round_trip()
    {
        Assert.True(ColorRgba.TryParseHex("#3B82F6", out var c));
        Assert.Equal(new ColorRgba(0x3B, 0x82, 0xF6), c);
        Assert.Equal("#3B82F6", c.ToHex());
        Assert.True(ColorRgba.TryParseHex("f00", out var red));
        Assert.Equal(new ColorRgba(255, 0, 0), red);
        Assert.False(ColorRgba.TryParseHex("nope", out _));
    }

    [Fact]
    public void Hsv_round_trip()
    {
        var original = new ColorRgba(200, 120, 40);
        ColorMath.RgbToHsv(original.R / 255.0, original.G / 255.0, original.B / 255.0, out var h, out var s, out var v);
        Assert.Equal(original, ColorMath.FromHsv(h, s, v));
    }
}
