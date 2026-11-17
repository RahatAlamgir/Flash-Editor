using FlashEditor.AI.Inpainting;
using FlashEditor.Core.Imaging;
using Xunit;

namespace FlashEditor.Core.Tests.Inpainting;

public class InpaintGeometryTests
{
    private static byte[] Mask(int w, int h, PixelRect rect)
    {
        var m = new byte[w * h];
        for (int y = rect.Y; y < rect.Bottom; y++) for (int x = rect.X; x < rect.Right; x++) m[y * w + x] = 255;
        return m;
    }

    [Fact]
    public void A_small_selection_gets_a_full_size_window_so_no_detail_is_lost()
    {
        var window = InpaintGeometry.PlanWindow(2000, 1500, new PixelRect(900, 700, 60, 80));
        Assert.Equal(512, window.Side);
        Assert.True(window.Left <= 900 && window.Right >= 960 && window.Top <= 700 && window.Bottom >= 780);   // the whole selection is inside
    }

    [Fact]
    public void A_big_selection_gets_a_window_with_room_around_it()
    {
        var window = InpaintGeometry.PlanWindow(4000, 3000, new PixelRect(1000, 800, 800, 500));
        Assert.Equal(1200, window.Side);                                                // one and a half times the longest side
        Assert.True(window.Left <= 1000 && window.Right >= 1800 && window.Top <= 800 && window.Bottom >= 1300);
    }

    [Fact]
    public void The_window_stays_on_the_picture_when_it_fits_there()
    {
        var corner = InpaintGeometry.PlanWindow(2000, 1500, new PixelRect(5, 5, 40, 40));
        Assert.Equal((0, 0), (corner.Left, corner.Top));
        var far = InpaintGeometry.PlanWindow(2000, 1500, new PixelRect(1950, 1450, 40, 40));
        Assert.Equal((2000 - 512, 1500 - 512), (far.Left, far.Top));
        var smallPicture = InpaintGeometry.PlanWindow(300, 200, new PixelRect(100, 80, 40, 30));           // smaller than the window: centered
        Assert.Equal(512, smallPicture.Side);
        Assert.True(smallPicture.Left < 0 && smallPicture.Right > 300);
    }

    [Fact]
    public void Widening_the_mask_grows_it_by_the_radius_and_nothing_else()
    {
        var mask = Mask(100, 100, new PixelRect(40, 40, 10, 10));
        var wide = InpaintGeometry.Dilate(mask, 100, 100, new PixelRect(40, 40, 10, 10), 5);
        Assert.Equal(255, wide[35 * 100 + 35]);                     // 5 pixels left and above the corner
        Assert.Equal(255, wide[54 * 100 + 54]);
        Assert.Equal(0, wide[34 * 100 + 45]);                       // 6 away: not covered
        Assert.Equal(0, wide[45 * 100 + 55]);
        Assert.Equal(255, mask[45 * 100 + 45]);                     // the original array is not changed
        Assert.Equal(0, mask[35 * 100 + 35]);
        Assert.Equal(0, InpaintGeometry.Dilate(mask, 100, 100, new PixelRect(40, 40, 10, 10), 0)[35 * 100 + 35]);
    }

    [Fact]
    public void The_widening_depends_on_the_size_of_the_selection()
    {
        Assert.Equal(4, InpaintGeometry.DilationFor(new PixelRect(0, 0, 20, 20)));
        Assert.Equal(30, InpaintGeometry.DilationFor(new PixelRect(0, 0, 2000, 900)));
        Assert.InRange(InpaintGeometry.DilationFor(new PixelRect(0, 0, 400, 300)), 4, 30);
    }

    [Fact]
    public void The_model_input_has_the_picture_as_planar_rgb_and_the_hole_as_ones()
    {
        var image = new PixelBuffer(512, 512);
        image.Fill(new ColorRgba(255, 0, 128));
        var mask = Mask(512, 512, new PixelRect(100, 100, 50, 50));
        var window = new InpaintWindow(0, 0, 512);
        var (tensor, hole) = InpaintGeometry.Prepare(image, mask, window);

        Assert.Equal(3 * 512 * 512, tensor.Length);
        Assert.Equal(512 * 512, hole.Length);
        Assert.Equal(1f, tensor[10 * 512 + 10], 3);                         // red plane
        Assert.Equal(0f, tensor[512 * 512 + 10 * 512 + 10], 3);            // green plane
        Assert.Equal(128 / 255f, tensor[2 * 512 * 512 + 10 * 512 + 10], 3); // blue plane
        Assert.Equal(1f, hole[120 * 512 + 120]);
        Assert.Equal(0f, hole[10 * 512 + 10]);
    }

    [Fact]
    public void A_window_larger_than_the_model_is_shrunk_and_the_hole_goes_with_it()
    {
        var image = new PixelBuffer(1200, 1200);
        image.Fill(new ColorRgba(10, 20, 30));
        var mask = Mask(1200, 1200, new PixelRect(400, 400, 400, 400));
        var window = new InpaintWindow(0, 0, 1024);                        // shown at half size
        var (tensor, hole) = InpaintGeometry.Prepare(image, mask, window);
        Assert.Equal(1f, hole[300 * 512 + 300]);                            // (600, 600) in the picture is inside the hole
        Assert.Equal(0f, hole[50 * 512 + 50]);
        Assert.InRange(tensor[100 * 512 + 100], 9f / 255, 11f / 255);       // colors survive the shrinking
    }

    [Fact]
    public void Where_the_window_sticks_out_of_the_picture_nothing_is_asked_to_be_filled()
    {
        var image = new PixelBuffer(300, 200);
        image.Fill(new ColorRgba(50, 60, 70));
        var mask = Mask(300, 200, new PixelRect(100, 80, 40, 30));
        var window = InpaintGeometry.PlanWindow(300, 200, new PixelRect(100, 80, 40, 30));
        var (tensor, hole) = InpaintGeometry.Prepare(image, mask, window);
        int outsideLeft = 5;                                               // the window starts left of the picture
        Assert.True(window.Left < 0);
        Assert.Equal(0f, hole[256 * 512 + outsideLeft]);
        Assert.Equal(50 / 255f, tensor[256 * 512 + outsideLeft], 3);       // the edge color is repeated there
    }

    [Fact]
    public void See_through_places_are_flattened_onto_white_for_the_model()
    {
        var image = new PixelBuffer(512, 512);                              // all transparent
        var (tensor, _) = InpaintGeometry.Prepare(image, new byte[512 * 512], new InpaintWindow(0, 0, 512));
        Assert.Equal(1f, tensor[0], 3);
        Assert.Equal(1f, tensor[512 * 512], 3);
    }

    [Fact]
    public void The_answer_is_blended_in_only_where_the_selection_was_with_a_soft_edge()
    {
        int w = 600, h = 600;
        var mask = Mask(w, h, new PixelRect(250, 250, 60, 60));
        var area = new PixelRect(250, 250, 60, 60);
        var window = new InpaintWindow(44, 44, 512);
        var answer = new float[3 * 512 * 512];
        for (int p = 0; p < 512 * 512; p++) { answer[p] = 0f; answer[512 * 512 + p] = 1f; answer[2 * 512 * 512 + p] = 0f; }   // the model says: green

        var result = InpaintGeometry.Compose(w, h, mask, area, answer, window);
        Assert.Equal(new ColorRgba(0, 255, 0), result.GetPixel(280, 280));  // fully shown inside
        Assert.Equal(0, result.GetPixel(100, 100).A);                       // nothing elsewhere
        Assert.Equal(0, result.GetPixel(250 - 8, 280).A);                   // the soft edge stays close to the selection
        var edge = result.GetPixel(250 - 2, 280);
        Assert.True(edge.A is > 0 and < 255, $"edge alpha {edge.A}");       // soft just outside
        Assert.True(result.GetPixel(250, 280).A > 200);
    }

    [Fact]
    public void The_bounds_of_a_mask_are_found_and_an_empty_mask_is_empty()
    {
        var mask = Mask(200, 100, new PixelRect(30, 20, 50, 40));
        Assert.Equal(new PixelRect(30, 20, 50, 40), Inpainter.BoundsOf(mask, 200, 100));
        Assert.True(Inpainter.BoundsOf(new byte[200 * 100], 200, 100).IsEmpty);
    }
}
