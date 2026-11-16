using FlashEditor.Core.Imaging;
using FlashEditor.Core.Sheets;

namespace FlashEditor.Core.Tests.Sheets;

public class SheetSplitterTests
{
    /// <summary>A 3 x 2 grid of colored squares (6 assets).</summary>
    private static PixelBuffer Sheet(ColorRgba background)
    {
        var sheet = TestImages.Solid(300, 200, background);
        var colors = new[] { new ColorRgba(220, 40, 40), new ColorRgba(40, 180, 60), new ColorRgba(40, 80, 220) };
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
                for (int y = 0; y < 60; y++)
                    for (int x = 0; x < 60; x++)
                        sheet.SetPixel(20 + col * 100 + x, 20 + row * 100 + y, colors[col]);
        return sheet;
    }

    [Fact]
    public void Finds_six_pieces_on_a_plain_background_in_reading_order()
    {
        var pieces = SheetSplitter.Split(Sheet(ColorRgba.White), new SplitSettings());
        Assert.Equal(6, pieces.Count);
        Assert.Equal(new PixelRect(20, 20, 60, 60), pieces[0].Bounds);
        Assert.Equal(new PixelRect(220, 120, 60, 60), pieces[5].Bounds);
        Assert.Equal(new ColorRgba(40, 180, 60), pieces[1].Pixels.GetPixel(30, 30));
    }

    [Fact]
    public void Works_on_a_transparent_background()
    {
        var pieces = SheetSplitter.Split(Sheet(ColorRgba.Transparent), new SplitSettings());
        Assert.Equal(6, pieces.Count);
    }

    [Fact]
    public void Close_parts_are_joined_into_one_piece()
    {
        var sheet = Sheet(ColorRgba.White);
        // A small "label" 5 px under the first square.
        for (int x = 30; x < 70; x++)
            for (int y = 85; y < 97; y++)
                sheet.SetPixel(x, y, ColorRgba.Black);

        Assert.Equal(6, SheetSplitter.Split(sheet, new SplitSettings(Gap: 8)).Count);   // label joins the square
        Assert.Equal(7, SheetSplitter.Split(sheet, new SplitSettings(Gap: 0)).Count);   // label on its own
    }

    [Fact]
    public void Background_inside_a_piece_becomes_transparent_with_padding()
    {
        var pieces = SheetSplitter.Split(Sheet(ColorRgba.White), new SplitSettings(Padding: 4));
        var first = pieces[0];
        Assert.Equal(new PixelRect(16, 16, 68, 68), first.Bounds);
        Assert.Equal(0, first.Pixels.GetPixel(1, 1).A);     // padding (white) is transparent
        Assert.Equal(255, first.Pixels.GetPixel(30, 30).A); // the item itself is kept
    }

    [Fact]
    public void With_a_selection_only_the_selected_areas_become_pieces()
    {
        var sheet = Sheet(ColorRgba.White);
        var selection = FlashEditor.Core.Selections.Selection.Combine(
            FlashEditor.Core.Selections.Selection.FromRectangle(new PixelRect(10, 10, 80, 80), 300, 200),       // first square
            FlashEditor.Core.Selections.Selection.FromRectangle(new PixelRect(210, 110, 80, 80), 300, 200),     // last square
            FlashEditor.Core.Selections.Selection.CombineMode.Add)!;

        var pieces = SheetSplitter.Split(sheet, new SplitSettings(), selection.Mask);
        Assert.Equal(2, pieces.Count);
        Assert.Equal(new PixelRect(10, 10, 80, 80), pieces[0].Bounds);
        Assert.Equal(0, pieces[0].Pixels.GetPixel(2, 2).A);      // white background made transparent
        Assert.Equal(255, pieces[0].Pixels.GetPixel(40, 40).A);  // the square itself
    }

    [Fact]
    public void Tiny_specks_are_ignored()
    {
        var sheet = Sheet(ColorRgba.White);
        sheet.SetPixel(150, 190, ColorRgba.Black);
        Assert.Equal(6, SheetSplitter.Split(sheet, new SplitSettings(Gap: 0)).Count);
    }
}
