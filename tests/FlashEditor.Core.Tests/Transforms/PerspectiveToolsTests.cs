using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.Tests.Transforms;

public class PerspectiveToolsTests
{
    [Fact]
    public void Without_a_tilt_the_whole_picture_stays()
        => Assert.Equal((300, 200), PerspectiveTools.LargestInsideRotated(300, 200, 0));

    [Theory]
    [InlineData(300, 200, 3)]
    [InlineData(300, 200, -10)]
    [InlineData(200, 300, 25)]
    [InlineData(500, 500, 45)]
    [InlineData(800, 100, 7)]
    public void The_rectangle_that_is_kept_lies_completely_inside_the_turned_picture(int width, int height, double degrees)
    {
        var (w, h) = PerspectiveTools.LargestInsideRotated(width, height, degrees);
        Assert.InRange(w, 1, width);
        Assert.InRange(h, 1, height);
        // Its four corners, put back into the unturned picture, must be inside it.
        double a = degrees * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        foreach (var (cx, cy) in new[] { (-w / 2.0, -h / 2.0), (w / 2.0, -h / 2.0), (w / 2.0, h / 2.0), (-w / 2.0, h / 2.0) })
        {
            double sx = cx * cos + cy * sin, sy = -cx * sin + cy * cos;
            Assert.True(Math.Abs(sx) <= width / 2.0 + 1.5 && Math.Abs(sy) <= height / 2.0 + 1.5, $"corner {cx},{cy} -> {sx:F1},{sy:F1}");
        }
    }

    [Fact]
    public void A_bigger_tilt_keeps_less_of_the_picture()
    {
        var small = PerspectiveTools.LargestInsideRotated(400, 300, 2);
        var big = PerspectiveTools.LargestInsideRotated(400, 300, 15);
        Assert.True(big.Width * big.Height < small.Width * small.Height);
    }

    [Fact]
    public void Straightening_with_the_corners_cut_leaves_no_empty_pixel_inside()
    {
        var picture = new PixelBuffer(200, 120);
        picture.Fill(new ColorRgba(10, 200, 30));
        var result = PerspectiveTools.Straighten(picture, 8, cropCorners: true);
        for (int y = 0; y < result.Height; y += 2)
            for (int x = 0; x < result.Width; x += 2)
                Assert.True(result.GetPixel(x, y).A >= 250, $"empty pixel at {x},{y}");
        Assert.True(result.Width < 200 && result.Height < 120);
    }

    [Fact]
    public void Straightening_without_cutting_keeps_the_whole_turned_picture()
    {
        var picture = new PixelBuffer(200, 120);
        picture.Fill(new ColorRgba(10, 200, 30));
        var result = PerspectiveTools.Straighten(picture, 8, cropCorners: false);
        Assert.True(result.Width > 200);
        Assert.Equal(0, result.GetPixel(0, 0).A);                      // empty corner
    }

    [Fact]
    public void A_perspective_crop_of_an_upright_rectangle_is_a_plain_crop()
    {
        var picture = new PixelBuffer(100, 80);
        for (int y = 0; y < 80; y++) for (int x = 0; x < 100; x++) picture.SetPixel(x, y, x >= 40 && x < 70 && y >= 20 && y < 50 ? new ColorRgba(255, 0, 0) : new ColorRgba(0, 0, 255));
        var quad = new[] { new PointD(40, 20), new PointD(70, 20), new PointD(70, 50), new PointD(40, 50) };
        var result = PerspectiveTools.PerspectiveCrop(picture, quad, 30, 30)!;
        Assert.Equal(new ColorRgba(255, 0, 0), result.GetPixel(15, 15));
        Assert.Equal(new ColorRgba(255, 0, 0), result.GetPixel(1, 1));
        Assert.Equal(new ColorRgba(255, 0, 0), result.GetPixel(28, 28));
    }

    [Fact]
    public void A_slanted_page_becomes_a_flat_rectangle()
    {
        // A white "page" seen at an angle on a dark picture: trapezium with the far (top) edge shorter.
        var picture = new PixelBuffer(200, 200);
        picture.Fill(new ColorRgba(20, 20, 20));
        var page = new[] { new PointD(60, 40), new PointD(140, 40), new PointD(170, 160), new PointD(30, 160) };
        for (int y = 40; y < 160; y++)
        {
            double t = (y - 40) / 120.0;
            double left = 60 + (30 - 60) * t, right = 140 + (170 - 140) * t;
            for (int x = (int)Math.Ceiling(left + 1); x < right - 1; x++) picture.SetPixel(x, y, new ColorRgba(250, 250, 250));
        }
        var (w, h) = PerspectiveTools.SuggestedSize(page);
        var result = PerspectiveTools.PerspectiveCrop(picture, page, w, h)!;
        // Everything in the flat result is page (white), also in the corners that were far apart before.
        foreach (var (x, y) in new[] { (3, 3), (w - 4, 3), (3, h - 4), (w - 4, h - 4), (w / 2, h / 2) })
            Assert.True(result.GetPixel(x, y).R > 230, $"({x},{y}) = {result.GetPixel(x, y).R}");
    }

    [Fact]
    public void Four_points_on_a_line_are_not_a_shape()
        => Assert.Null(PerspectiveTools.PerspectiveCrop(new PixelBuffer(10, 10), new[] { new PointD(0, 0), new PointD(1, 1), new PointD(2, 2), new PointD(3, 3) }, 10, 10));
}
