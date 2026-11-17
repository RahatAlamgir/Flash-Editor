using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using Xunit;

namespace FlashEditor.Core.Tests.Objects;

public class ObjectStyleTests
{
    private static ShapeObject Fancy() => new(new ObjectBox(10, 10, 100, 50, 30), ShapeKind.Heart, new ColorRgba(255, 0, 0), new ColorRgba(0, 0, 255), 6)
    {
        Shadow = ShadowStyle.Default with { Size = 1.5 },
        ThreeD = ThreeDStyle.Default,
        Gradient = GradientStyle.Two(new ColorRgba(255, 0, 0), new ColorRgba(255, 255, 0), 0),
        GradientBorder = true,
    };

    [Fact]
    public void A_look_goes_onto_another_shape_without_changing_its_size_or_form()
    {
        var plain = new ShapeObject(new ObjectBox(300, 200, 40, 40, 90), ShapeKind.Star, new ColorRgba(1, 2, 3), null, 0, Points: 8);
        var styled = (ShapeObject)ObjectStyle.From(Fancy()).ApplyTo(plain);

        Assert.Equal(plain.Box, styled.Box);
        Assert.Equal(ShapeKind.Star, styled.Shape);
        Assert.Equal(8, styled.Points);
        Assert.Equal(Fancy().Shadow, styled.Shadow);
        Assert.Equal(Fancy().ThreeD, styled.ThreeD);
        Assert.Equal(Fancy().Gradient, styled.Gradient);
        Assert.True(styled.GradientBorder);
        Assert.Equal(new ColorRgba(255, 0, 0), styled.Fill);
        Assert.Equal(new ColorRgba(0, 0, 255), styled.Stroke);
        Assert.Equal(6, styled.StrokeWidth);
    }

    [Fact]
    public void Shadow_and_3d_go_onto_any_kind_but_colors_and_gradient_only_onto_the_same_kind()
    {
        var text = new TextObject(new ObjectBox(0, 0, 100, 30), "Hi", "Segoe UI", 20, new ColorRgba(9, 9, 9));
        var result = (TextObject)ObjectStyle.From(Fancy()).ApplyTo(text);
        Assert.NotNull(result.Shadow);
        Assert.NotNull(result.ThreeD);
        Assert.Null(result.Gradient);                          // a shape's gradient does not go onto text
        Assert.Equal(new ColorRgba(9, 9, 9), result.Color);

        var fancyText = text with { Color = new ColorRgba(200, 0, 0), Gradient = GradientStyle.Two(new ColorRgba(1, 1, 1), new ColorRgba(2, 2, 2)), Shadow = ShadowStyle.Default };
        var onOtherText = (TextObject)ObjectStyle.From(fancyText).ApplyTo(text with { Text = "Other" });
        Assert.Equal("Other", onOtherText.Text);
        Assert.Equal(new ColorRgba(200, 0, 0), onOtherText.Color);
        Assert.Equal(fancyText.Gradient, onOtherText.Gradient);
    }

    [Fact]
    public void A_look_without_effects_removes_them()
    {
        var bare = new ShapeObject(new ObjectBox(0, 0, 10, 10), ShapeKind.Rectangle, new ColorRgba(5, 5, 5), null, 0);
        var cleaned = ObjectStyle.From(bare).ApplyTo(Fancy());
        Assert.Null(cleaned.Shadow);
        Assert.Null(cleaned.ThreeD);
        Assert.Null(cleaned.Gradient);
    }
}
