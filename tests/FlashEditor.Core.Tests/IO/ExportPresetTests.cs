using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;

namespace FlashEditor.Core.Tests.IO;

public class ExportPresetTests
{
    private static readonly ExportPreset Square = new("Test", "Square", 100, 100);

    private static PixelBuffer Picture(int w, int h, ColorRgba color)
    {
        var p = new PixelBuffer(w, h);
        p.Fill(color);
        return p;
    }

    [Fact]
    public void Every_preset_has_a_group_a_name_and_a_real_size()
    {
        Assert.True(ExportPresets.All.Count >= 15);
        Assert.All(ExportPresets.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Group));
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.InRange(p.Width, 100, 10000);
            Assert.InRange(p.Height, 100, 10000);
        });
        Assert.Contains(ExportPresets.All, p => p.Name == "YouTube thumbnail" && p.Width == 1280 && p.Height == 720);
    }

    [Fact]
    public void Fit_shows_the_whole_picture_and_fills_the_rest_with_the_background()
    {
        var wide = Picture(200, 100, new ColorRgba(255, 0, 0));
        var result = ExportPresets.Compose(wide, Square, ExportFit.Fit, new ColorRgba(0, 0, 255));
        Assert.Equal((100, 100), (result.Width, result.Height));
        Assert.Equal(new ColorRgba(0, 0, 255), result.GetPixel(50, 10));           // above the picture: background
        Assert.Equal(new ColorRgba(255, 0, 0), result.GetPixel(50, 50));           // the picture (100 x 50) in the middle
        Assert.Equal(new ColorRgba(0, 0, 255), result.GetPixel(50, 90));
    }

    [Fact]
    public void Fit_without_a_background_leaves_the_rest_see_through()
    {
        var result = ExportPresets.Compose(Picture(200, 100, new ColorRgba(255, 0, 0)), Square, ExportFit.Fit, null);
        Assert.Equal(0, result.GetPixel(50, 5).A);
        Assert.Equal(255, result.GetPixel(50, 50).A);
    }

    [Fact]
    public void Fill_covers_the_whole_size_and_cuts_off_the_sides()
    {
        var wide = new PixelBuffer(200, 100);
        for (int y = 0; y < 100; y++) for (int x = 0; x < 200; x++) wide.SetPixel(x, y, x < 50 ? new ColorRgba(255, 0, 0) : x >= 150 ? new ColorRgba(0, 0, 255) : new ColorRgba(0, 255, 0));
        var result = ExportPresets.Compose(wide, Square, ExportFit.Fill, null);
        Assert.Equal((100, 100), (result.Width, result.Height));
        Assert.All(new[] { (0, 0), (99, 0), (0, 99), (99, 99), (50, 50) }, p => Assert.Equal(new ColorRgba(0, 255, 0), result.GetPixel(p.Item1, p.Item2)));   // only the middle part is left
    }

    [Fact]
    public void Stretch_changes_the_shape_to_the_preset()
    {
        var result = ExportPresets.Compose(Picture(200, 100, new ColorRgba(9, 9, 9)), new ExportPreset("T", "Tall", 40, 120), ExportFit.Stretch, null);
        Assert.Equal((40, 120), (result.Width, result.Height));
        Assert.Equal(255, result.GetPixel(20, 60).A);
    }

    [Fact]
    public void A_small_picture_is_enlarged_to_the_preset_size()
    {
        var result = ExportPresets.Compose(Picture(10, 10, new ColorRgba(1, 2, 3)), new ExportPreset("T", "Big", 300, 300), ExportFit.Fit, null);
        Assert.Equal((300, 300), (result.Width, result.Height));
        Assert.Equal(255, result.GetPixel(150, 150).A);
    }
}
