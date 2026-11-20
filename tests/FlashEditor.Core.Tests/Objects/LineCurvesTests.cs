using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

public class LineCurvesTests
{
    [Fact]
    public void A_line_without_a_bend_has_its_handles_on_its_ends()
    {
        var (a, c1, c2, b) = LineCurves.Controls(100, 6, 0, LineBendStyle.Arc);
        Assert.Equal(new PointD(0, 3), a);
        Assert.Equal(a, c1);
        Assert.Equal(b, c2);
        Assert.Equal(new PointD(100, 3), b);
    }

    [Fact]
    public void An_arc_bows_to_one_side_and_the_other_way_for_a_negative_bend()
    {
        var up = LineCurves.Controls(100, 6, -0.5, LineBendStyle.Arc);
        var down = LineCurves.Controls(100, 6, 0.5, LineBendStyle.Arc);
        Assert.True(up.Handle1.Y < 3 && up.Handle2.Y < 3);
        Assert.True(down.Handle1.Y > 3 && down.Handle2.Y > 3);
        Assert.Equal(up.Handle1.Y - 3, -(down.Handle1.Y - 3), 6);
    }

    [Fact]
    public void A_wave_goes_to_one_side_and_then_the_other()
    {
        var w = LineCurves.Controls(100, 6, 0.5, LineBendStyle.Wave);
        Assert.True(w.Handle1.Y > 3);
        Assert.True(w.Handle2.Y < 3);
    }

    [Fact]
    public void The_reach_grows_with_the_bend_and_is_zero_for_a_straight_line()
    {
        Assert.Equal(0, LineCurves.Reach(200, 0, LineBendStyle.Arc));
        Assert.True(LineCurves.Reach(200, 1, LineBendStyle.Arc) > LineCurves.Reach(200, 0.5, LineBendStyle.Arc));
    }

    [Fact]
    public void Arrow_heads_of_a_straight_line_point_straight_out_of_both_ends()
    {
        var (a, c1, c2, b) = LineCurves.Controls(100, 6, 0, LineBendStyle.Arc);
        var (start, end) = LineCurves.TipDirections(a, c1, c2, b);
        Assert.Equal(-1, start.X, 6);
        Assert.Equal(1, end.X, 6);
    }

    [Fact]
    public void Arrow_heads_of_a_curve_follow_its_ends()
    {
        var nodes = new[]
        {
            new CurveNode(new PointD(0, 0), new PointD(0, 0), new PointD(0, 10)),      // leaves downwards
            new CurveNode(new PointD(10, 20), new PointD(10, 10), new PointD(10, 20)),  // arrives going downwards
        };
        var (start, end) = CurvePath.EndDirections(nodes);
        Assert.Equal(0, start.X, 6); Assert.Equal(-1, start.Y, 6);                    // points out of the start, upwards
        Assert.Equal(0, end.X, 6); Assert.Equal(1, end.Y, 6);                         // points out of the end, downwards
    }
}
