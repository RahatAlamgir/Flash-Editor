using FlashEditor.Core.Layout;
using Xunit;

namespace FlashEditor.Core.Tests.Layout;

public class CollageLayoutTests
{
    [Fact]
    public void A_2_by_2_grid_fills_the_canvas_with_equal_gaps()
    {
        var layout = CollageLayout.All.First(l => l.Name == "2 x 2 grid");
        var cells = layout.Place(1000, 600, padding: 20, gap: 10);
        Assert.Equal(4, cells.Count);
        Assert.Equal((20, 20, 475, 275), cells[0]);
        Assert.Equal(1000 - 20, cells[3].X + cells[3].Width, 6);          // the last frame ends at the border
        Assert.Equal(600 - 20, cells[3].Y + cells[3].Height, 6);
        Assert.Equal(10, cells[1].X - (cells[0].X + cells[0].Width), 6);   // gap between neighbors
    }

    [Fact]
    public void A_large_cell_covers_several_cells_and_the_gaps_between_them()
    {
        var layout = CollageLayout.All.First(l => l.Name == "One large, two small");
        var cells = layout.Place(1000, 600, padding: 0, gap: 20);
        Assert.Equal(3, cells.Count);
        Assert.Equal(600, cells[0].Height, 6);                             // the big one spans both rows
        Assert.Equal(cells[1].Height * 2 + 20, cells[0].Height, 6);
    }

    [Fact]
    public void Every_layout_has_frames_that_stay_inside_the_canvas_and_do_not_overlap()
    {
        foreach (var layout in CollageLayout.All)
        {
            var cells = layout.Place(800, 500, 16, 12);
            Assert.NotEmpty(cells);
            foreach (var c in cells)
            {
                Assert.True(c.X >= 0 && c.Y >= 0 && c.X + c.Width <= 800.0001 && c.Y + c.Height <= 500.0001, layout.Name);
                Assert.True(c.Width > 0 && c.Height > 0);
            }
            for (int i = 0; i < cells.Count; i++)
                for (int j = i + 1; j < cells.Count; j++)
                {
                    var a = cells[i]; var b = cells[j];
                    bool overlap = a.X < b.X + b.Width - 0.01 && b.X < a.X + a.Width - 0.01 && a.Y < b.Y + b.Height - 0.01 && b.Y < a.Y + a.Height - 0.01;
                    Assert.False(overlap, $"{layout.Name}: frames {i} and {j} overlap");
                }
        }
    }
}
