using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Selections;

/// <summary>Selections from masks (magic wand, AI subject), ellipses, and adding / subtracting.</summary>
public class MaskSelectionTests
{
    [Fact]
    public void Square_mask_outline_is_one_four_corner_loop()
    {
        var mask = new byte[10 * 10];
        for (int y = 2; y < 6; y++)
            for (int x = 3; x < 8; x++)
                mask[y * 10 + x] = 255;

        var loops = MaskOutline.Trace(mask, 10, 10);
        Assert.Single(loops);
        Assert.Equal(4, loops[0].Count);
        Assert.Equal(3, loops[0].Min(p => p.X));
        Assert.Equal(8, loops[0].Max(p => p.X));
    }

    [Fact]
    public void Ring_mask_gives_outer_and_inner_loops()
    {
        var mask = new byte[10 * 10];
        for (int y = 1; y < 9; y++)
            for (int x = 1; x < 9; x++)
                mask[y * 10 + x] = (byte)(x is >= 4 and < 6 && y is >= 4 and < 6 ? 0 : 255);
        Assert.Equal(2, MaskOutline.Trace(mask, 10, 10).Count);
    }

    [Fact]
    public void Ellipse_selection_is_round()
    {
        var s = Selection.FromEllipse(new PixelRect(0, 0, 20, 20), 20, 20)!;
        Assert.True(s.IsSelected(10, 10));
        Assert.False(s.IsSelected(0, 0));   // corner is outside a circle
        Assert.True(s.IsSelected(10, 1));
    }

    [Fact]
    public void Add_and_subtract_combine_selections()
    {
        var a = Selection.FromRectangle(new PixelRect(0, 0, 10, 10), 30, 30);
        var b = Selection.FromRectangle(new PixelRect(5, 5, 10, 10), 30, 30);

        var union = Selection.Combine(a, b, Selection.CombineMode.Add)!;
        Assert.True(union.IsSelected(1, 1) && union.IsSelected(13, 13));

        var cut = Selection.Combine(a, b, Selection.CombineMode.Subtract)!;
        Assert.True(cut.IsSelected(1, 1));
        Assert.False(cut.IsSelected(7, 7));
        Assert.NotEmpty(cut.Outline);
    }

    [Fact]
    public void Export_crops_before_resizing()
    {
        var image = TestImages.Numbered(40, 20);
        var settings = new ExportSettings(ImageFileFormat.Png, 100, 10, 10, Crop: new PixelRect(5, 5, 10, 10));
        var result = ImageExporter.Resize(image, settings);
        Assert.Equal((10, 10), (result.Width, result.Height));
        Assert.Equal(image.GetPixel(5, 5), result.GetPixel(0, 0));
    }
}
