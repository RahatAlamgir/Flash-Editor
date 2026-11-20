using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

public class TextPresetTests
{
    private static TextObject Sample(double size = 64)
        => new(new ObjectBox(10, 20, 300, 80), "Hello\nthere", "Segoe UI", size, new ColorRgba(1, 2, 3), Align: TextAlign.Right, FlipX: true);

    [Fact]
    public void Every_design_has_its_own_name()
    {
        Assert.True(TextPreset.All.Count >= 12);
        Assert.Equal(TextPreset.All.Count, TextPreset.All.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void A_design_keeps_the_words_the_place_and_the_alignment()
    {
        foreach (var preset in TextPreset.All)
        {
            var result = preset.ApplyTo(Sample());
            Assert.Equal("Hello\nthere", result.Text);
            Assert.Equal(new ObjectBox(10, 20, 300, 80), result.Box);
            Assert.Equal(TextAlign.Right, result.Align);
            Assert.True(result.FlipX);
            Assert.Equal(64, result.FontSize);
            Assert.Equal(preset.Font, result.FontFamily);
        }
    }

    [Fact]
    public void Effects_grow_and_shrink_with_the_size_of_the_text()
    {
        var neon = TextPreset.All.First(p => p.Name == "Neon Pink");
        double big = neon.ApplyTo(Sample(128)).Glow!.Size;
        double normal = neon.ApplyTo(Sample(64)).Glow!.Size;
        Assert.Equal(normal * 2, big, 6);
    }

    [Fact]
    public void Applying_a_plain_design_removes_the_effects_of_a_fancy_one()
    {
        var fire = TextPreset.All.First(p => p.Name == "Fire").ApplyTo(Sample());
        Assert.NotNull(fire.Gradient);
        var clean = TextPreset.All.First(p => p.Name == "Clean").ApplyTo(fire);
        Assert.Null(clean.Gradient);
        Assert.Null(clean.Glow);
        Assert.Null(clean.Shadow);
        Assert.Equal(0, clean.OutlineWidth);
    }
}
