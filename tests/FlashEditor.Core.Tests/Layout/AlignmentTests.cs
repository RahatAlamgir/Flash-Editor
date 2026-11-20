using FlashEditor.Core.Imaging;
using FlashEditor.Core.Layout;

namespace FlashEditor.Core.Tests.Layout;

public class AlignmentTests
{
    private static readonly PixelRect Canvas = new(0, 0, 1000, 600);

    [Fact]
    public void One_box_lines_up_with_the_canvas()
    {
        var box = new[] { new PixelRect(100, 50, 200, 100) };
        Assert.Equal((-100, 0), Alignment.Align(box, Canvas, AlignKind.Left)[0]);
        Assert.Equal((700, 0), Alignment.Align(box, Canvas, AlignKind.Right)[0]);
        Assert.Equal((300, 0), Alignment.Align(box, Canvas, AlignKind.CenterHorizontally)[0]);
        Assert.Equal((0, -50), Alignment.Align(box, Canvas, AlignKind.Top)[0]);
        Assert.Equal((0, 450), Alignment.Align(box, Canvas, AlignKind.Bottom)[0]);
        Assert.Equal((0, 200), Alignment.Align(box, Canvas, AlignKind.CenterVertically)[0]);
    }

    [Fact]
    public void A_corner_or_the_middle_moves_in_both_directions_at_once()
    {
        var box = new[] { new PixelRect(100, 50, 200, 100) };
        Assert.Equal((-100, -50), Alignment.AlignBoth(box, Canvas, AlignKind.Left, AlignKind.Top)[0]);
        Assert.Equal((700, 450), Alignment.AlignBoth(box, Canvas, AlignKind.Right, AlignKind.Bottom)[0]);
        Assert.Equal((300, 200), Alignment.AlignBoth(box, Canvas, AlignKind.CenterHorizontally, AlignKind.CenterVertically)[0]);
        Assert.Equal((-100, 450), Alignment.AlignBoth(box, Canvas, AlignKind.Left, AlignKind.Bottom)[0]);
    }

    [Fact]
    public void Several_boxes_go_to_a_corner_of_the_area_they_cover_together()
    {
        var boxes = new[] { new PixelRect(100, 100, 50, 50), new PixelRect(300, 200, 100, 100) };
        var moves = Alignment.AlignBoth(boxes, Canvas, AlignKind.Right, AlignKind.Top);
        Assert.Equal((250, 0), moves[0]);
        Assert.Equal((0, -100), moves[1]);
    }

    [Fact]
    public void Several_boxes_line_up_with_the_area_they_cover_together()
    {
        var boxes = new[] { new PixelRect(100, 100, 50, 50), new PixelRect(400, 300, 100, 80), new PixelRect(250, 200, 20, 20) };
        var left = Alignment.Align(boxes, Canvas, AlignKind.Left);
        Assert.Equal(new[] { (0, 0), (-300, 0), (-150, 0) }, left);                    // everything goes to x = 100
        var right = Alignment.Align(boxes, Canvas, AlignKind.Right);
        Assert.Equal(new[] { (350, 0), (0, 0), (230, 0) }, right);                     // everything ends at x = 500
        var middle = Alignment.Align(boxes, Canvas, AlignKind.CenterVertically);       // the group spans y 100..380, middle 240
        Assert.Equal(new[] { 240 - 125, 240 - 340, 240 - 210 }, middle.Select(m => m.Dy).ToArray());
        Assert.All(middle, m => Assert.Equal(0, m.Dx));
    }

    [Fact]
    public void After_aligning_the_edges_really_match()
    {
        var boxes = new[] { new PixelRect(10, 10, 33, 20), new PixelRect(300, 90, 71, 45), new PixelRect(77, 400, 5, 5) };
        foreach (var kind in Enum.GetValues<AlignKind>())
        {
            var moved = Alignment.Align(boxes, Canvas, kind);
            var after = boxes.Select((b, i) => new PixelRect(b.X + moved[i].Dx, b.Y + moved[i].Dy, b.Width, b.Height)).ToArray();
            switch (kind)
            {
                case AlignKind.Left: Assert.Single(after.Select(a => a.X).Distinct()); break;
                case AlignKind.Right: Assert.Single(after.Select(a => a.Right).Distinct()); break;
                case AlignKind.Top: Assert.Single(after.Select(a => a.Y).Distinct()); break;
                case AlignKind.Bottom: Assert.Single(after.Select(a => a.Bottom).Distinct()); break;
                case AlignKind.CenterHorizontally: Assert.True(after.Select(a => a.X * 2 + a.Width).Max() - after.Select(a => a.X * 2 + a.Width).Min() <= 1); break;
                case AlignKind.CenterVertically: Assert.True(after.Select(a => a.Y * 2 + a.Height).Max() - after.Select(a => a.Y * 2 + a.Height).Min() <= 1); break;
            }
        }
    }

    [Fact]
    public void Distributing_makes_the_gaps_equal_and_keeps_the_outer_boxes_in_place()
    {
        var boxes = new[] { new PixelRect(0, 0, 100, 50), new PixelRect(130, 0, 60, 50), new PixelRect(500, 0, 100, 50), new PixelRect(210, 0, 40, 50) };
        var moved = Alignment.Distribute(boxes, DistributeKind.Horizontally);
        var after = boxes.Select((b, i) => new PixelRect(b.X + moved[i].Dx, b.Y + moved[i].Dy, b.Width, b.Height)).OrderBy(b => b.X).ToArray();
        Assert.Equal(0, after[0].X);
        Assert.Equal(600, after[^1].Right);                                             // the outer boxes did not move
        var gaps = Enumerable.Range(1, after.Length - 1).Select(i => after[i].X - after[i - 1].Right).ToArray();
        Assert.True(gaps.Max() - gaps.Min() <= 1, string.Join(",", gaps));              // equal, give or take a pixel of rounding
        Assert.All(moved, m => Assert.Equal(0, m.Dy));
    }

    [Fact]
    public void Distributing_vertically_and_with_too_few_boxes()
    {
        var boxes = new[] { new PixelRect(0, 0, 50, 100), new PixelRect(0, 120, 50, 100), new PixelRect(0, 500, 50, 100) };
        var moved = Alignment.Distribute(boxes, DistributeKind.Vertically);
        Assert.Equal((0, 0), moved[0]);
        Assert.Equal((0, 130), moved[1]);                                               // gaps of (600 - 300) / 2 = 150: it starts at 250
        Assert.Equal((0, 0), moved[2]);

        Assert.All(Alignment.Distribute(boxes.Take(2).ToArray(), DistributeKind.Vertically), m => Assert.Equal((0, 0), m));
        Assert.Empty(Alignment.Align(Array.Empty<PixelRect>(), Canvas, AlignKind.Left));
    }
}
