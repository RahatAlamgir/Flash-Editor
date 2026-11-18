using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using Xunit;

namespace FlashEditor.Core.Tests.Painting;

public class LiquifyTests
{
    /// <summary>A white picture with a black vertical line at x = 50.</summary>
    private static PixelBuffer LineOnWhite()
    {
        var p = new PixelBuffer(100, 100);
        p.Fill(new ColorRgba(255, 255, 255));
        for (int y = 0; y < 100; y++) p.SetPixel(50, y, new ColorRgba(0, 0, 0));
        return p;
    }

    [Fact]
    public void Pushing_moves_what_is_under_the_brush_the_way_the_brush_moves()
    {
        var p = LineOnWhite();
        var area = Liquify.Apply(p, 50, 50, 30, 1.0, LiquifyMode.Push, dx: 10, dy: 0);
        Assert.False(area.IsEmpty);
        Assert.True(p.GetPixel(58, 50).R < 128, "the line was pushed to the right");
        Assert.True(p.GetPixel(50, 50).R > 200, "and is no longer where it was");
        Assert.Equal(255, p.GetPixel(10, 50).R);                       // outside the brush nothing changed
        Assert.Equal(0, p.GetPixel(50, 5).R);                          // the line outside the brush stays
    }

    [Fact]
    public void A_stronger_brush_moves_things_further()
    {
        var weak = LineOnWhite();
        var strong = LineOnWhite();
        Liquify.Apply(weak, 50, 50, 30, 0.3, LiquifyMode.Push, 10, 0);
        Liquify.Apply(strong, 50, 50, 30, 1.0, LiquifyMode.Push, 10, 0);
        Assert.True(strong.GetPixel(58, 50).R < weak.GetPixel(58, 50).R);
    }

    [Fact]
    public void Bloat_makes_a_shape_bigger_and_pucker_makes_it_smaller()
    {
        var disc = new PixelBuffer(100, 100);
        disc.Fill(new ColorRgba(255, 255, 255));
        for (int y = 0; y < 100; y++) for (int x = 0; x < 100; x++)
            if ((x - 50) * (x - 50) + (y - 50) * (y - 50) < 12 * 12) disc.SetPixel(x, y, new ColorRgba(0, 0, 0));
        int Black(PixelBuffer p) { int n = 0; for (int i = 0; i < p.Data.Length; i += 4) if (p.Data[i] < 128) n++; return n; }

        var bigger = disc.Clone();
        var smaller = disc.Clone();
        Liquify.Apply(bigger, 50, 50, 35, 1.0, LiquifyMode.Bloat);
        Liquify.Apply(smaller, 50, 50, 35, 1.0, LiquifyMode.Pucker);
        Assert.True(Black(bigger) > Black(disc));
        Assert.True(Black(smaller) < Black(disc));
    }

    [Fact]
    public void Nothing_happens_without_movement_a_radius_or_strength()
    {
        var p = LineOnWhite();
        Assert.True(Liquify.Apply(p, 50, 50, 30, 1.0, LiquifyMode.Push, 0, 0).IsEmpty);
        Assert.True(Liquify.Apply(p, 50, 50, 0, 1.0, LiquifyMode.Bloat).IsEmpty);
        Assert.True(Liquify.Apply(p, 50, 50, 30, 0, LiquifyMode.Bloat).IsEmpty);
        Assert.True(p.ContentEquals(LineOnWhite()));
    }

    [Fact]
    public void A_brush_at_the_edge_of_the_picture_does_not_go_outside_it()
    {
        var p = LineOnWhite();
        var area = Liquify.Apply(p, 2, 2, 30, 1.0, LiquifyMode.Bloat);
        Assert.True(area.X >= 0 && area.Y >= 0 && area.Right <= 100 && area.Bottom <= 100);
    }
}
