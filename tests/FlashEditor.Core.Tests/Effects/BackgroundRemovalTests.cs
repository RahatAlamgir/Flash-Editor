using FlashEditor.Core.Effects.Background;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using Xunit.Abstractions;

namespace FlashEditor.Core.Tests.Effects;

/// <summary>Background removal on photo-like pictures (not just flat colors).</summary>
public class BackgroundRemovalTests
{
    private readonly ITestOutputHelper _output;

    public BackgroundRemovalTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> Scenes() => RealisticBackgrounds.All().Select(s => new object[] { s.Name });

    [Theory]
    [MemberData(nameof(Scenes))]
    public void Removes_realistic_backgrounds_with_default_settings(string name)
    {
        var scene = RealisticBackgrounds.All().First(s => s.Name == name);
        var effect = new BackgroundRemovalEffect();
        var result = effect.Apply(scene.Image, effect.CreateDefaultValues());

        var (removed, kept) = RealisticBackgrounds.Score(scene, result);
        _output.WriteLine($"{name}: background removed {removed:P1}, subject kept {kept:P1}");
        Dump(name, scene.Image, result);

        Assert.True(removed >= 0.95, $"only {removed:P1} of the background was removed");
        Assert.True(kept >= 0.98, $"only {kept:P1} of the subject was kept");
    }

    /// <summary>Set FLASH_DUMP to a folder to save before/after PNGs for a visual check.</summary>
    private static void Dump(string name, PixelBuffer before, PixelBuffer after)
    {
        var dir = Environment.GetEnvironmentVariable("FLASH_DUMP");
        if (string.IsNullOrEmpty(dir)) return;
        string safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        ImageEncoder.Save(before, Path.Combine(dir, safe + "_before.png"), ImageFileFormat.Png);
        ImageEncoder.Save(after, Path.Combine(dir, safe + "_after.png"), ImageFileFormat.Png);
    }
}
