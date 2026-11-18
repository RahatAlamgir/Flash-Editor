using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using Xunit;

namespace FlashEditor.Core.Tests.Objects;

public class CurvePathTests
{
    private static CurveNode[] Arch() => new[]
    {
        CurveNode.Corner(new PointD(0, 100)),
        CurveNode.Smooth(new PointD(50, 0), new PointD(80, 0)),
        CurveNode.Corner(new PointD(100, 100)),
    };

    [Fact]
    public void Straight_segments_are_lines_between_the_points()
    {
        var points = CurvePath.Flatten(new[] { CurveNode.Corner(new PointD(0, 0)), CurveNode.Corner(new PointD(10, 20)) }, false, 4);
        Assert.Equal(5, points.Count);
        Assert.Equal(new PointD(5, 10), points[2]);
    }

    [Fact]
    public void A_closed_curve_has_one_more_segment_than_an_open_one()
    {
        Assert.Equal(2, CurvePath.SegmentCount(3, false));
        Assert.Equal(3, CurvePath.SegmentCount(3, true));
        Assert.Equal(0, CurvePath.SegmentCount(1, true));
    }

    [Fact]
    public void The_box_hugs_the_curve_and_not_its_handles()
    {
        var (x, y, w, h) = CurvePath.Bounds(Arch(), false);
        Assert.Equal(0, x, 6);
        Assert.Equal(100, w, 6);
        Assert.True(y <= 1e-6 + 0 && y >= -1e-6 || y < 5);            // the curve reaches the top point
        Assert.InRange(h, 90, 100.0001);
    }

    [Fact]
    public void Fitting_a_curve_gives_a_box_and_points_as_fractions_of_it()
    {
        var (box, unit) = CurvePath.Fit(new[] { CurveNode.Corner(new PointD(10, 20)), CurveNode.Corner(new PointD(110, 70)) }, false);
        Assert.Equal((10, 20, 100, 50), (box.X, box.Y, box.Width, box.Height));
        Assert.Equal(new PointD(0, 0), unit[0].Point);
        Assert.Equal(new PointD(1, 1), unit[1].Point);
    }

    [Fact]
    public void A_flat_horizontal_curve_still_gets_a_box_one_pixel_thick()
    {
        var (box, unit) = CurvePath.Fit(new[] { CurveNode.Corner(new PointD(0, 5)), CurveNode.Corner(new PointD(40, 5)) }, false);
        Assert.Equal(1, box.Height);
        Assert.Equal(0, unit[0].Point.Y);
    }

    [Fact]
    public void Splitting_a_segment_adds_a_point_and_keeps_the_curve_the_same()
    {
        var nodes = Arch();
        var before = CurvePath.Flatten(nodes, false, 60);
        var split = CurvePath.Split(nodes, false, 1, 0.4);
        Assert.Equal(4, split.Count);
        var after = CurvePath.Flatten(split, false, 60);
        // Every point of the old curve is (nearly) on the new one.
        foreach (var p in before.Where((_, i) => i % 7 == 0))
            Assert.True(CurvePath.Nearest(split, false, p, 400).Distance < 0.5, $"{p} is off the new curve");
        Assert.Equal(nodes[2].Point, split[3].Point);
    }

    [Fact]
    public void The_nearest_place_is_found()
    {
        var (segment, t, distance) = CurvePath.Nearest(new[] { CurveNode.Corner(new PointD(0, 0)), CurveNode.Corner(new PointD(100, 0)) }, false, new PointD(25, 3));
        Assert.Equal(0, segment);
        Assert.InRange(t, 0.0, 1.0);
        Assert.Equal(3, distance, 1);
    }

    [Fact]
    public void A_corner_becomes_smooth_and_a_smooth_point_becomes_a_corner_again()
    {
        var nodes = new[] { CurveNode.Corner(new PointD(0, 0)), CurveNode.Corner(new PointD(50, 30)), CurveNode.Corner(new PointD(100, 0)) };
        var smooth = CurvePath.ToggleSmooth(nodes, false, 1);
        Assert.True(smooth[1].IsSmooth);
        Assert.Equal(nodes[1].Point, smooth[1].Point);
        var sharp = CurvePath.ToggleSmooth(smooth, false, 1);
        Assert.False(sharp[1].HasHandleIn || sharp[1].HasHandleOut);
    }

    [Fact]
    public void A_smooth_node_has_opposite_handles_of_equal_length()
    {
        var n = CurveNode.Smooth(new PointD(10, 10), new PointD(20, 15));
        Assert.Equal(new PointD(0, 5), n.HandleIn);
        Assert.True(n.IsSmooth);
    }
}
