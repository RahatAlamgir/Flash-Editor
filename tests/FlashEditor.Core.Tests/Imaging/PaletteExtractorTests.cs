using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Imaging;

public class PaletteExtractorTests
{
    [Fact]
    public void The_main_colors_come_back_most_common_first()
    {
        var p = new PixelBuffer(100, 100);
        p.Fill(new ColorRgba(255, 0, 0));                                          // 100 % red...
        for (int y = 0; y < 100; y++) for (int x = 0; x < 30; x++) p.SetPixel(x, y, new ColorRgba(0, 0, 255));   // ...then 30 % blue
        var colors = PaletteExtractor.Extract(p, 3);
        Assert.Equal(2, colors.Count);
        Assert.True(colors[0].R > 200 && colors[0].B < 60);
        Assert.True(colors[1].B > 200 && colors[1].R < 60);
    }

    [Fact]
    public void A_plain_picture_gives_one_color()
    {
        var p = new PixelBuffer(20, 20);
        p.Fill(new ColorRgba(10, 200, 90));
        var colors = PaletteExtractor.Extract(p, 5);
        Assert.Single(colors);
        Assert.Equal(new ColorRgba(10, 200, 90), colors[0]);
    }

    [Fact]
    public void See_through_pixels_are_ignored()
        => Assert.Empty(PaletteExtractor.Extract(new PixelBuffer(10, 10), 4));
}
