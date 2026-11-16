using FlashEditor.AI.Effects;
using FlashEditor.AI.Models;
using FlashEditor.AI.Segmentation;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.Tests.Effects;
using Xunit.Abstractions;

namespace FlashEditor.Core.Tests.AI;

public class AiBackgroundTests
{
    private readonly ITestOutputHelper _output;

    public AiBackgroundTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Input_tensor_is_planar_rgb_and_normalized()
    {
        var image = TestImages.Solid(4, 4, new ColorRgba(255, 0, 51));
        var tensor = MaskOps.ToInputTensor(image, 4, new[] { 0.5f, 0.5f, 0.5f }, new[] { 1f, 1f, 1f });
        Assert.Equal(3 * 16, tensor.Length);
        Assert.Equal(0.5f, tensor[0], 3);          // R plane: 1.0 - 0.5
        Assert.Equal(-0.5f, tensor[16], 3);        // G plane: 0.0 - 0.5
        Assert.Equal(-0.3f, tensor[32], 3);        // B plane: 0.2 - 0.5
    }

    [Fact]
    public void Normalize_stretches_to_zero_one()
    {
        var mask = MaskOps.Normalize(new[] { 2f, 4f, 6f });
        Assert.Equal(new[] { 0f, 0.5f, 1f }, mask);
        Assert.All(MaskOps.Normalize(new[] { 3f, 3f }), v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Resize_keeps_values_in_range_and_size()
    {
        var mask = MaskOps.Resize(new[] { 0f, 1f, 1f, 0f }, 2, 2, 7, 5);
        Assert.Equal(35, mask.Length);
        Assert.All(mask, v => Assert.InRange(v, 0f, 1f));
    }

    [Fact]
    public void Adjust_defaults_keep_soft_mask_and_full_sharpness_makes_it_hard()
    {
        var soft = new[] { 0.2f, 0.5f, 0.8f };
        MaskOps.Adjust(soft, 0, 0);
        Assert.Equal(0.2f, soft[0], 3);

        var hard = new[] { 0.45f, 0.55f };
        MaskOps.Adjust(hard, 1, 0);
        Assert.Equal(0f, hard[0]);
        Assert.Equal(1f, hard[1]);

        var shrunk = new[] { 1f, 0.97f, 0f };
        MaskOps.Adjust(shrunk, 0.3, -1); // strongest shrink, default sharpness
        Assert.Equal(1f, shrunk[0]);     // the sure subject stays fully visible
        Assert.Equal(0f, shrunk[2]);

        var grown = new[] { 0.3f };
        MaskOps.Adjust(grown, 1, 1); // grow: keep more
        Assert.Equal(1f, grown[0]);
    }

    /// <summary>
    /// A small photo in the corner of a big transparent canvas (a pasted picture) must give the same
    /// result as the photo on its own: the empty area must not confuse the model.
    /// </summary>
    [Fact]
    public void Real_model_works_on_a_small_picture_in_a_big_empty_canvas_when_installed()
    {
        var model = ModelInfo.IsNetGeneralUse;
        var store = new ModelStore();
        if (!store.IsInstalled(model) && FindAppModelsFolder() is { } appModels) store = new ModelStore(appModels);
        if (!store.IsInstalled(model)) { _output.WriteLine("SKIPPED: AI model not installed"); return; }

        using var segmenter = new SubjectSegmenter(model, store.PathOf(model));
        var effect = new AiBackgroundRemovalEffect(segmenter);
        foreach (var scene in RealisticBackgrounds.All())
        {
            var canvas = new PixelBuffer(scene.Image.Width * 3, scene.Image.Height * 2); // mostly empty
            canvas.CopyFrom(scene.Image, 0, 0);
            var result = effect.Apply(canvas, effect.CreateDefaultValues()).Crop(scene.Image.Bounds);
            var (removed, kept) = RealisticBackgrounds.Score(scene, result);
            _output.WriteLine($"{scene.Name} (in a big canvas): background removed {removed:P1}, subject kept {kept:P1}");
            Assert.True(removed >= 0.9, $"{scene.Name}: only {removed:P1} of the background was removed");
            Assert.True(kept >= 0.9, $"{scene.Name}: only {kept:P1} of the subject was kept");
        }
    }

    [Fact]
    public void A_two_file_model_counts_as_installed_only_when_both_files_are_complete()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flash-model-test-" + Guid.NewGuid().ToString("N"));
        var model = ModelInfo.RealEsrganX4Plus;
        var folder = Path.Combine(dir, model.Folder!);
        Directory.CreateDirectory(folder);
        var store = new ModelStore(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "real_esrgan_x4plus.onnx"), new byte[model.ArchiveFiles!["real_esrgan_x4plus.onnx"]]);
            Assert.False(store.IsInstalled(model)); // weights file missing

            using (var f = File.Create(Path.Combine(folder, "real_esrgan_x4plus.data"))) f.SetLength(model.ArchiveFiles["real_esrgan_x4plus.data"]);
            Assert.True(store.IsInstalled(model));
            Assert.EndsWith("real_esrgan_x4plus.onnx", store.PathOf(model));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_wrong_model_file_is_rejected_and_deleted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flash-model-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "bad.onnx");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

        Assert.Throws<InvalidDataException>(() => ModelStore.Verify(ModelInfo.IsNetGeneralUse, path));
        Assert.False(File.Exists(path));
        Directory.Delete(dir, true);
    }

    /// <summary>The "models" folder next to the built app (found by walking up to the solution folder).</summary>
    private static string? FindAppModelsFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "FlashEditor.slnx"))) continue;
            var models = Path.Combine(dir.FullName, "src", "FlashEditor.App", "bin", "Debug", "net10.0-windows", "models");
            return Directory.Exists(models) ? models : null;
        }
        return null;
    }

    /// <summary>
    /// Runs the real model when it is installed on this PC (it is a 170 MB download, so it is not
    /// part of the repository). Without the model the test only reports that it was skipped.
    /// </summary>
    [Fact]
    public void Real_model_separates_subject_from_background_when_installed()
    {
        var model = ModelInfo.IsNetGeneralUse;
        var store = new ModelStore();
        if (!store.IsInstalled(model) && FindAppModelsFolder() is { } appModels)
            store = new ModelStore(appModels); // the model sitting next to the built app
        if (!store.IsInstalled(model))
        {
            _output.WriteLine("SKIPPED: AI model not installed in " + store.Folder);
            return;
        }

        using var segmenter = new SubjectSegmenter(model, store.PathOf(model));
        var effect = new AiBackgroundRemovalEffect(segmenter);
        foreach (var scene in RealisticBackgrounds.All())
        {
            var result = effect.Apply(scene.Image, effect.CreateDefaultValues());
            var (removed, kept) = RealisticBackgrounds.Score(scene, result);
            _output.WriteLine($"{scene.Name}: background removed {removed:P1}, subject kept {kept:P1}");

            var dir = Environment.GetEnvironmentVariable("FLASH_DUMP");
            if (!string.IsNullOrEmpty(dir))
            {
                string safe = new string(scene.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
                ImageEncoder.Save(result, Path.Combine(dir, safe + "_ai.png"), ImageFileFormat.Png);
            }
            Assert.True(removed >= 0.9, $"{scene.Name}: only {removed:P1} of the background was removed");
            Assert.True(kept >= 0.9, $"{scene.Name}: only {kept:P1} of the subject was kept");
        }
    }
}
