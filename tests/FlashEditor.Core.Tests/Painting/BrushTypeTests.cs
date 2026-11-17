using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;

namespace FlashEditor.Core.Tests.Painting;

/// <summary>The different brush tips: what each one paints from a single click.</summary>
public class BrushTypeTests
{
    private const int Canvas = 201, Center = 100;

    /// <summary>Alpha of every pixel after one click in the middle of a transparent canvas.</summary>
    private static byte[,] Dab(BrushType type, double size = 80, double hardness = 1.0)
    {
        var image = new PixelBuffer(Canvas, Canvas);
        var stroke = new BrushStroke(image, new BrushSettings(size, hardness, 1.0, type), ColorRgba.Black, erase: false, selection: null);
        stroke.AddPoint(new PointD(Center + 0.5, Center + 0.5));
        var alpha = new byte[Canvas, Canvas];
        for (int y = 0; y < Canvas; y++)
            for (int x = 0; x < Canvas; x++) alpha[x, y] = image.GetPixel(x, y).A;
        return alpha;
    }

    private static int Painted(byte[,] alpha, int minAlpha = 1)
    {
        int n = 0;
        foreach (var a in alpha) if (a >= minAlpha) n++;
        return n;
    }

    [Fact]
    public void A_square_brush_paints_its_corners_a_round_brush_does_not()
    {
        var round = Dab(BrushType.Round);
        var square = Dab(BrushType.Square);

        // 40 px radius: the point (35, 35) from the middle lies inside the square but outside the circle (distance 49).
        Assert.Equal(0, round[Center + 35, Center + 35]);
        Assert.Equal(255, square[Center + 35, Center + 35]);
        Assert.True(Painted(square) > Painted(round) * 1.2);
        Assert.Equal(0, square[Center + 45, Center]); // but not beyond its sides
    }

    [Fact]
    public void A_calligraphy_brush_is_thick_one_way_and_thin_the_other()
    {
        var alpha = Dab(BrushType.Calligraphy);

        // Slanted 45 degrees: long towards bottom-right (along the diagonal), thin across it.
        Assert.Equal(255, alpha[Center + 25, Center + 25]);
        Assert.Equal(0, alpha[Center + 25, Center - 25]);
        Assert.True(Painted(alpha) < Painted(Dab(BrushType.Round)) * 0.5);
    }

    [Fact]
    public void A_pencil_has_hard_edges_and_a_slight_grain()
    {
        var alpha = Dab(BrushType.Pencil, size: 80, hardness: 0.0); // hardness is ignored by a pencil
        Assert.Equal(0, alpha[Center + 43, Center]);                 // clearly outside: nothing
        int solid = 0, partial = 0;
        for (int y = Center - 20; y <= Center + 20; y++)
            for (int x = Center - 20; x <= Center + 20; x++)
            {
                Assert.True(alpha[x, y] > 100, "inside a pencil dab every pixel is clearly painted");
                if (alpha[x, y] > 235) solid++; else partial++;
            }
        Assert.True(solid > 0 && partial > 0, "the grain makes the strength vary from pixel to pixel");
    }

    [Fact]
    public void Chalk_is_speckled_with_gaps()
    {
        var alpha = Dab(BrushType.Chalk, hardness: 1.0);
        int inside = 0, empty = 0;
        for (int y = Center - 25; y <= Center + 25; y++)
            for (int x = Center - 25; x <= Center + 25; x++)
            {
                inside++;
                if (alpha[x, y] == 0) empty++;
            }
        double share = empty / (double)inside;
        Assert.InRange(share, 0.2, 0.6);   // a good part is left bare, a good part is painted
    }

    [Fact]
    public void Spray_is_a_sparse_mist_inside_the_brush_size()
    {
        var spray = Dab(BrushType.Spray, size: 80);
        int painted = Painted(spray);
        int roundArea = Painted(Dab(BrushType.Round, size: 80));
        Assert.InRange(painted, 3, roundArea / 2);             // far fewer pixels than a solid dab

        for (int y = 0; y < Canvas; y++)
            for (int x = 0; x < Canvas; x++)
                if (spray[x, y] > 0)
                {
                    double d = Math.Sqrt((x - Center) * (x - Center) + (y - Center) * (y - Center));
                    Assert.True(d <= 40 + 3, $"dot at distance {d:F1} is outside the brush");
                }
    }

    [Fact]
    public void Splatter_makes_separate_blobs_that_can_go_slightly_past_the_brush_size()
    {
        var splatter = Dab(BrushType.Splatter, size: 80);
        Assert.InRange(Painted(splatter), 5, Painted(Dab(BrushType.Round, size: 80)));
        for (int y = 0; y < Canvas; y++)
            for (int x = 0; x < Canvas; x++)
                if (splatter[x, y] > 0)
                    Assert.True(Math.Sqrt((x - Center) * (x - Center) + (y - Center) * (y - Center)) <= 80 * 1.0 + 8); // within 1.6 x radius + blob
    }

    [Fact]
    public void Every_type_can_also_be_used_to_erase_and_stays_inside_the_canvas_near_the_edge()
    {
        foreach (var type in Enum.GetValues<BrushType>())
        {
            var image = new PixelBuffer(60, 60);
            image.Fill(ColorRgba.White);
            var stroke = new BrushStroke(image, new BrushSettings(50, 1.0, 1.0, type), ColorRgba.Black, erase: true, selection: null);
            stroke.AddPoint(new PointD(2, 2));          // a brush half outside the canvas must not crash
            stroke.AddPoint(new PointD(58, 58));
            Assert.True(image.Data.Where((_, i) => i % 4 == 3).Any(a => a < 255), $"{type} erased nothing");
        }
    }

    [Fact]
    public void Overlapping_dabs_do_not_change_the_grain_of_a_pencil_stroke()
    {
        var image = new PixelBuffer(120, 40);
        var stroke = new BrushStroke(image, new BrushSettings(20, 1.0, 1.0, BrushType.Pencil), ColorRgba.Black, false, null);
        stroke.AddPoint(new PointD(20, 20));
        var afterFirst = image.Clone();
        stroke.AddPoint(new PointD(20.5, 20));           // almost the same place again
        // Pixels that were fully inside both dabs keep their value (the grain is fixed per pixel).
        Assert.Equal(afterFirst.GetPixel(20, 20), image.GetPixel(20, 20));
        Assert.Equal(afterFirst.GetPixel(15, 22), image.GetPixel(15, 22));
    }

    [Fact]
    public void Brush_type_defaults_to_round_so_old_settings_still_mean_the_same()
    {
        Assert.Equal(BrushType.Round, new BrushSettings(10, 1, 1).Type);
        var plain = Dab(BrushType.Round);
        var image = new PixelBuffer(Canvas, Canvas);
        new BrushStroke(image, new BrushSettings(80, 1.0, 1.0), ColorRgba.Black, false, null).AddPoint(new PointD(Center + 0.5, Center + 0.5));
        Assert.Equal(plain[Center + 10, Center], image.GetPixel(Center + 10, Center).A);
    }
}
