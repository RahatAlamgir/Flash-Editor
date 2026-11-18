using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using Xunit;

namespace FlashEditor.Core.Tests.Documents;

public class ToneAdjustmentTests
{
    private static ColorRgba Adjusted(AdjustmentStyle style, byte r, byte g, byte b)
    {
        byte rr = r, gg = g, bb = b;
        style.Apply(ref bb, ref gg, ref rr);
        return new ColorRgba(rr, gg, bb);
    }

    [Fact]
    public void A_new_Levels_or_Curves_layer_changes_nothing()
    {
        Assert.True(AdjustmentStyle.NewLevels.IsNeutral);
        Assert.True(AdjustmentStyle.NewCurves.IsNeutral);
        Assert.Equal(new ColorRgba(10, 120, 250), Adjusted(AdjustmentStyle.NewCurves, 10, 120, 250));
    }

    [Fact]
    public void Levels_black_and_white_points_stretch_the_tones()
    {
        var style = new AdjustmentStyle(Kind: AdjustmentKind.Levels, LevelsBlack: 50, LevelsWhite: 200);
        Assert.Equal(new ColorRgba(0, 0, 0), Adjusted(style, 40, 50, 20));              // at or below the black point
        Assert.Equal(new ColorRgba(255, 255, 255), Adjusted(style, 200, 220, 255));     // at or above the white point
        var middle = Adjusted(style, 125, 125, 125);                                      // halfway between the points
        Assert.InRange(middle.R, 126, 129);
    }

    [Fact]
    public void Levels_gamma_lightens_or_darkens_the_middle_tones_but_not_the_ends()
    {
        var lighter = new AdjustmentStyle(LevelsGamma: 2);
        var darker = new AdjustmentStyle(LevelsGamma: 0.5);
        Assert.True(Adjusted(lighter, 100, 100, 100).R > 100);
        Assert.True(Adjusted(darker, 100, 100, 100).R < 100);
        Assert.Equal(0, Adjusted(lighter, 0, 0, 0).R);
        Assert.Equal(255, Adjusted(darker, 255, 255, 255).R);
    }

    [Fact]
    public void Levels_output_range_lifts_black_and_dims_white()
    {
        var style = new AdjustmentStyle(OutputBlack: 30, OutputWhite: 220);
        Assert.Equal(30, Adjusted(style, 0, 0, 0).R);
        Assert.Equal(220, Adjusted(style, 255, 255, 255).R);
    }

    [Fact]
    public void A_curve_through_points_bends_the_tones_smoothly()
    {
        var curve = new[] { new CurvePoint(0, 0), new CurvePoint(128, 180), new CurvePoint(255, 255) };       // lifts the middle
        var table = CurveMath.BuildTable(curve);
        Assert.Equal(0, table[0]);
        Assert.Equal(255, table[255]);
        Assert.InRange(table[128], 178, 182);
        for (int i = 1; i < 256; i++) Assert.True(table[i] >= table[i - 1], "the curve never goes down here: " + i);
    }

    [Fact]
    public void A_curve_never_overshoots_between_its_points()
    {
        var steep = new[] { new CurvePoint(0, 0), new CurvePoint(100, 10), new CurvePoint(120, 250), new CurvePoint(255, 255) };
        var table = CurveMath.BuildTable(steep);
        Assert.All(table, v => Assert.InRange(v, 0, 255));
        for (int i = 1; i < 256; i++) Assert.True(table[i] >= table[i - 1]);
    }

    [Fact]
    public void A_curve_that_ends_are_lowered_makes_the_picture_flatter()
    {
        var style = new AdjustmentStyle(Kind: AdjustmentKind.Curves, Curve: new[] { new CurvePoint(0, 40), new CurvePoint(255, 215) });
        Assert.Equal(40, Adjusted(style, 0, 0, 0).R);
        Assert.Equal(215, Adjusted(style, 255, 255, 255).R);
    }

    [Fact]
    public void A_channel_curve_changes_only_its_channel()
    {
        var style = new AdjustmentStyle(Kind: AdjustmentKind.Curves, CurveRed: new[] { new CurvePoint(0, 100), new CurvePoint(255, 255) });
        var result = Adjusted(style, 0, 50, 90);
        Assert.Equal(100, result.R);
        Assert.Equal(50, result.G);
        Assert.Equal(90, result.B);
    }

    [Fact]
    public void Points_can_be_added_and_removed_but_not_the_end_points()
    {
        var curve = CurveMath.WithPoint(null, new CurvePoint(100, 150));
        Assert.Equal(3, curve.Count);
        Assert.Equal(new CurvePoint(100, 150), curve[1]);
        var moved = CurveMath.WithPoint(curve, new CurvePoint(101, 160));            // close to an existing point: that one moves
        Assert.Equal(3, moved.Count);
        Assert.Equal(160, moved[1].Y);
        Assert.Equal(2, CurveMath.WithoutPoint(moved, 1).Count);
        Assert.Equal(3, CurveMath.WithoutPoint(moved, 0).Count);                      // the first point stays
        Assert.Equal(3, CurveMath.WithoutPoint(moved, 2).Count);                      // and so does the last
    }

    [Fact]
    public void The_strength_of_an_adjustment_layer_still_blends_the_tone_change()
    {
        var style = new AdjustmentStyle(LevelsGamma: 2);
        byte r = 100, g = 100, b = 100;
        style.Apply(ref b, ref g, ref r, 0.5);
        var full = Adjusted(style, 100, 100, 100).R;
        Assert.InRange(r, 100, full);
        Assert.NotEqual(100, r);
    }

    [Fact]
    public void Levels_and_curves_are_saved_with_the_project()
    {
        var layer = new Layer("Curves", 10, 10) { Adjustment = new AdjustmentStyle(Kind: AdjustmentKind.Curves, LevelsBlack: 12, LevelsGamma: 1.4, Curve: new[] { new CurvePoint(0, 0), new CurvePoint(90, 130), new CurvePoint(255, 255) }) };
        var doc = new Document(10, 10);
        doc.SetLayers(new[] { new Layer("bg", 10, 10), layer }, layer);
        string path = Path.Combine(Path.GetTempPath(), "flash-tone-" + Guid.NewGuid().ToString("N") + ".flash");
        try
        {
            ProjectWriter.Save(doc, path);
            var loaded = ProjectReader.Open(path).Layers[1].Adjustment!;
            Assert.Equal(AdjustmentKind.Curves, loaded.Kind);
            Assert.Equal(12, loaded.LevelsBlack);
            Assert.Equal(1.4, loaded.LevelsGamma);
            Assert.Equal(3, loaded.Curve!.Count);
            Assert.Equal(new CurvePoint(90, 130), loaded.Curve[1]);
        }
        finally { File.Delete(path); }
    }
}
