using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.Tests.Transforms;

public class TransformTests
{
    [Fact]
    public void Rotate90_clockwise_moves_top_left_to_top_right()
    {
        var image = TestImages.Numbered(3, 2);
        var rotated = ImageTransforms.Rotate90Clockwise(image);
        Assert.Equal((2, 3), (rotated.Width, rotated.Height));
        Assert.Equal(image.GetPixel(0, 0), rotated.GetPixel(1, 0));
        Assert.Equal(image.GetPixel(0, 1), rotated.GetPixel(0, 0));
    }

    [Fact]
    public void Rotating_four_times_gives_original()
    {
        var image = TestImages.Numbered(5, 3);
        var result = image;
        for (int i = 0; i < 4; i++) result = ImageTransforms.Rotate90CounterClockwise(result);
        Assert.True(image.ContentEquals(result));
    }

    [Fact]
    public void Flip_horizontal_twice_is_identity()
    {
        var image = TestImages.Numbered(4, 4);
        Assert.Equal(image.GetPixel(0, 2), ImageTransforms.FlipHorizontal(image).GetPixel(3, 2));
        Assert.True(image.ContentEquals(ImageTransforms.FlipHorizontal(ImageTransforms.FlipHorizontal(image))));
    }

    [Fact]
    public void Flip_vertical_swaps_rows()
    {
        var image = TestImages.Numbered(4, 3);
        Assert.Equal(image.GetPixel(1, 0), ImageTransforms.FlipVertical(image).GetPixel(1, 2));
    }

    [Theory]
    [InlineData(100, 50, 30, 15)]
    [InlineData(10, 10, 40, 25)]
    public void Resize_produces_requested_size(int w, int h, int newW, int newH)
    {
        var result = Resampler.Resize(TestImages.Numbered(w, h), newW, newH);
        Assert.Equal((newW, newH), (result.Width, result.Height));
    }

    [Fact]
    public void Resizing_a_solid_color_keeps_the_color()
    {
        var color = new ColorRgba(40, 120, 200);
        var result = Resampler.Resize(TestImages.Solid(64, 64, color), 17, 9);
        Assert.Equal(color, result.GetPixel(8, 4));
    }

    [Fact]
    public void Arbitrary_rotation_grows_the_canvas()
    {
        var (w, h) = ImageTransforms.RotatedSize(100, 100, 45);
        Assert.InRange(w, 141, 142);
        Assert.InRange(h, 141, 142);

        var rotated = ImageTransforms.Rotate(TestImages.Solid(100, 100, ColorRgba.White), 45);
        Assert.Equal(0, rotated.GetPixel(0, 0).A);                          // new corner is transparent
        Assert.Equal(255, rotated.GetPixel(w / 2, h / 2).A);                // center keeps the image
    }
}
