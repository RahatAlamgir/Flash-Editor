using FlashEditor.AI.Segmentation;

namespace FlashEditor.Core.Tests.AI;

public class ClickSegmenterTests
{
    [Fact]
    public void A_click_is_scaled_to_the_model_size_and_gets_a_padding_point()
    {
        var (coords, labels) = ClickSegmenter.BuildPrompt(new ClickPrompt(new[] { (500.0, 250.0, true) }), 2048, 1024);
        Assert.Equal(1f, labels[0]);
        Assert.Equal(250f, coords[0]);          // 2048 wide -> scale 0.5
        Assert.Equal(125f, coords[1]);
        Assert.Equal(-1f, labels[1]);
    }

    [Fact]
    public void A_box_gives_its_two_corners_in_order_and_no_padding_point()
    {
        var (coords, labels) = ClickSegmenter.BuildPrompt(new ClickPrompt(Array.Empty<(double, double, bool)>(), (300, 400, 100, 200)), 1024, 1024);
        Assert.Equal(new float[] { 2, 3 }, labels);
        Assert.Equal(new float[] { 100, 200, 300, 400 }, coords);
    }

    [Fact]
    public void A_click_beside_the_object_is_labelled_zero()
    {
        var (_, labels) = ClickSegmenter.BuildPrompt(new ClickPrompt(new[] { (1.0, 1.0, true), (5.0, 5.0, false) }), 1024, 1024);
        Assert.Equal(new float[] { 1, 0, -1 }, labels);
    }
}
