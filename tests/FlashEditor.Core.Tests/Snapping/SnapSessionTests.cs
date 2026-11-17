using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Snapping;
using Xunit;

namespace FlashEditor.Core.Tests.Snapping;

public class SnapSessionTests
{
    private static readonly double[] None = Array.Empty<double>();

    private static Document Doc() => new(400, 300);

    private static Layer AddLayer(Document d, string name) { var l = new Layer(name, 400, 300); d.Layers.Add(l); return l; }

    private static SnapSession Session(Document d, SnapSettings s, double[]? gx = null, double[]? gy = null, IEnumerable<Layer>? exclude = null)
        => new(d, exclude ?? Array.Empty<Layer>(), gx ?? None, gy ?? None, s);

    private static readonly SnapSettings CanvasOnly = new(false, false, true, false);

    [Fact]
    public void A_box_near_the_canvas_edge_snaps_to_it()
    {
        var r = Session(Doc(), CanvasOnly).SnapBox(4, 100, 104, 150, 6);
        Assert.Equal(-4, r.Dx);
        Assert.Contains(0.0, r.LinesX);
    }

    [Fact]
    public void A_box_far_from_everything_does_not_snap()
    {
        var r = Session(Doc(), CanvasOnly).SnapBox(60, 60, 120, 100, 6);
        Assert.Equal(0, r.Dx);
        Assert.Equal(0, r.Dy);
        Assert.False(r.Snapped);
    }

    [Fact]
    public void The_center_of_a_box_snaps_to_the_center_of_the_canvas()
    {
        var r = Session(Doc(), CanvasOnly).SnapBox(150, 40, 247, 90, 6);   // center x = 198.5, canvas center 200
        Assert.Equal(1.5, r.Dx, 6);
        Assert.Contains(200.0, r.LinesX);
    }

    [Fact]
    public void The_nearest_line_wins()
    {
        var r = Session(Doc(), new SnapSettings(false, false, false, true), gx: new[] { 100.0, 103.0 }).SnapBox(101, 0, 150, 10, 6);
        Assert.Equal(-1, r.Dx);                                           // left edge 101 -> guide 100 (1 away) rather than 103 (2 away)
    }

    [Fact]
    public void Guides_snap_only_when_guide_snapping_is_on()
    {
        var off = Session(Doc(), new SnapSettings(false, false, false, false), gx: new[] { 100.0 }).SnapBox(98, 0, 140, 10, 6);
        Assert.False(off.Snapped);
        var on = Session(Doc(), new SnapSettings(false, false, false, true), gx: new[] { 100.0 }).SnapBox(98, 0, 140, 10, 6);
        Assert.Equal(2, on.Dx);
    }

    [Fact]
    public void The_grid_snaps_without_a_visible_line_to_draw()
    {
        var r = Session(Doc(), new SnapSettings(false, true, false, false, 50)).SnapBox(97, 53, 140, 90, 6);
        Assert.Equal(3, r.Dx);                                            // 97 -> 100
        Assert.Equal(-3, r.Dy);                                           // 53 -> 50
        Assert.Empty(r.LinesX);
        Assert.Empty(r.LinesY);
    }

    [Fact]
    public void Other_objects_are_snap_targets_but_the_dragged_one_is_not()
    {
        var doc = Doc();
        var a = AddLayer(doc, "A");
        var b = AddLayer(doc, "B");
        a.Pixels.SetPixel(300, 200, new ColorRgba(1, 2, 3));              // a small painted layer: bounds (300,200)-(301,201)
        b.Pixels.SetPixel(50, 50, new ColorRgba(1, 2, 3));

        var settings = new SnapSettings(true, false, false, false);
        var other = Session(doc, settings, exclude: new[] { b }).SnapBox(296, 10, 299, 20, 6);   // right edge 299 -> 300 (layer A's left edge)
        Assert.Equal(1, other.Dx);

        var self = Session(doc, settings, exclude: new[] { a, b }).SnapBox(296, 10, 299, 20, 6);
        Assert.False(self.Snapped);
    }

    [Fact]
    public void A_point_snaps_to_lines_too()
    {
        var r = Session(Doc(), new SnapSettings(false, true, true, false, 25)).SnapPoint(new PointD(398, 27), 6);
        Assert.Equal(2, r.Dx);                                            // 398 -> 400 (canvas edge / grid)
        Assert.Equal(-2, r.Dy);                                           // 27 -> 25 (grid)
    }

    // ---- equal spacing

    private static (Document Doc, Layer[] Layers) Row(params (int X, int Y, int W, int H)[] boxes)
    {
        var doc = new Document(1000, 600);
        var layers = new List<Layer>();
        foreach (var (x, y, w, h) in boxes)
        {
            var layer = new Layer("L", 1000, 600);
            for (int py = y; py < y + h; py++) for (int px = x; px < x + w; px++) layer.Pixels.SetPixel(px, py, new ColorRgba(1, 2, 3));
            doc.Layers.Add(layer);
            layers.Add(layer);
        }
        return (doc, layers.ToArray());
    }

    private static readonly SnapSettings SpacingOnly = new(false, false, false, false, 50, true);

    [Fact]
    public void An_object_keeps_the_same_gap_as_the_gaps_between_the_others()
    {
        // three objects 50 wide with 20 px between them: 0-50, 70-120, 140-190
        var (doc, _) = Row((0, 100, 50, 50), (70, 100, 50, 50), (140, 100, 50, 50));
        var r = Session(doc, SpacingOnly).SnapBox(214, 100, 264, 150, 6);       // dragged 50x50 object, 24 px right of the last one
        Assert.Equal(-4, r.Dx);                                                  // -> left edge 210 = 190 + 20
        Assert.Contains(r.Gaps, g => g.Horizontal && g.Value == 20 && g.From == 190);
        Assert.True(r.Gaps.Count(g => g.Value == 20) >= 2);                      // the new gap and the gap it copies
    }

    [Fact]
    public void An_object_snaps_into_the_middle_between_two_others()
    {
        var (doc, _) = Row((0, 100, 50, 50), (200, 100, 50, 50));                // 150 px of room
        var r = Session(doc, SpacingOnly).SnapBox(98, 100, 148, 150, 6);         // 50 wide: equal gaps of 50 -> left edge 100
        Assert.Equal(2, r.Dx);
        Assert.Equal(2, r.Gaps.Count(g => g.Value == 50));
    }

    [Fact]
    public void Objects_in_other_rows_do_not_count()
    {
        var (doc, _) = Row((0, 400, 50, 50), (70, 400, 50, 50), (140, 400, 50, 50));
        var r = Session(doc, SpacingOnly).SnapBox(214, 100, 264, 150, 6);        // this object is far above them
        Assert.False(r.Snapped);
    }

    [Fact]
    public void Equal_spacing_works_in_the_vertical_direction_too()
    {
        var (doc, _) = Row((100, 0, 50, 40), (100, 60, 50, 40), (100, 120, 50, 40)); // 20 px gaps downwards
        var r = Session(doc, SpacingOnly).SnapBox(100, 183, 150, 223, 6);        // 3 px too low: the last object ends at 160, so the top edge should be 180
        Assert.Equal(0, r.Dx);
        Assert.Equal(-3, r.Dy, 6);
        Assert.Contains(r.Gaps, g => !g.Horizontal && g.Value == 20);
    }

    [Fact]
    public void Equal_spacing_can_be_switched_off()
    {
        var (doc, _) = Row((0, 100, 50, 50), (70, 100, 50, 50), (140, 100, 50, 50));
        var r = Session(doc, new SnapSettings(false, false, false, false, 50, Spacing: false)).SnapBox(214, 100, 264, 150, 6);
        Assert.False(r.Snapped);
    }

    [Fact]
    public void Far_from_the_equal_gap_nothing_happens()
    {
        var (doc, _) = Row((0, 100, 50, 50), (70, 100, 50, 50), (140, 100, 50, 50));
        Assert.False(Session(doc, SpacingOnly).SnapBox(230, 100, 280, 150, 6).Snapped);   // 40 px gap, 20 wanted
    }
}
