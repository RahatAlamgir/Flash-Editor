using FlashEditor.AI.Models;

namespace FlashEditor.Core.Tests.AI;

public class ModelStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "flash-model-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static ModelInfo Single(long size = 100) => new("Test model", "test.onnx", "https://example.invalid/test.onnx", size, "", 8, new[] { 0f }, new[] { 1f });

    private static ModelInfo Archive() => new("Zip model", "z.onnx", "https://example.invalid/z.zip", 500, "", 8, new[] { 0f }, new[] { 1f },
        new Dictionary<string, long> { ["z.onnx"] = 10, ["z.data"] = 20 }, "zmodel");

    private void Write(string relative, int bytes)
    {
        string path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    [Fact]
    public void A_model_that_is_not_there_uses_no_space_and_deleting_it_does_nothing()
    {
        var store = new ModelStore(_folder);
        Assert.False(store.IsInstalled(Single()));
        Assert.Equal(0, store.BytesOnDisk(Single()));
        store.Delete(Single());                                   // no error
    }

    [Fact]
    public void An_installed_model_can_be_measured_and_deleted()
    {
        var store = new ModelStore(_folder);
        Write("test.onnx", 100);
        Assert.True(store.IsInstalled(Single()));
        Assert.Equal(100, store.BytesOnDisk(Single()));

        store.Delete(Single());
        Assert.False(store.IsInstalled(Single()));
        Assert.False(File.Exists(Path.Combine(_folder, "test.onnx")));
        Assert.Equal(0, store.BytesOnDisk(Single()));
    }

    [Fact]
    public void A_half_finished_download_counts_as_space_used_and_is_removed_with_the_model()
    {
        var store = new ModelStore(_folder);
        Write("test.onnx.part", 40);
        Assert.False(store.IsInstalled(Single()));                // incomplete: not usable
        Assert.Equal(40, store.BytesOnDisk(Single()));            // but it takes space, so it can be deleted
        store.Delete(Single());
        Assert.Equal(0, store.BytesOnDisk(Single()));
    }

    [Fact]
    public void Only_the_half_finished_download_is_removed_after_a_cancelled_download()
    {
        var store = new ModelStore(_folder);
        Write("test.onnx", 100);
        Write("test.onnx.part", 40);
        store.DeletePartialDownload(Single());
        Assert.True(store.IsInstalled(Single()));                 // the complete file stays
        Assert.False(File.Exists(Path.Combine(_folder, "test.onnx.part")));
    }

    [Fact]
    public void A_model_made_of_several_files_in_a_folder_is_deleted_with_its_folder()
    {
        var store = new ModelStore(_folder);
        Write(Path.Combine("zmodel", "z.onnx"), 10);
        Write(Path.Combine("zmodel", "z.data"), 20);
        Assert.True(store.IsInstalled(Archive()));
        Assert.Equal(30, store.BytesOnDisk(Archive()));

        store.Delete(Archive());
        Assert.False(store.IsInstalled(Archive()));
        Assert.False(Directory.Exists(Path.Combine(_folder, "zmodel")));
    }

    [Fact]
    public void Other_files_in_the_models_folder_are_never_touched()
    {
        var store = new ModelStore(_folder);
        Write("test.onnx", 100);
        Write("other.onnx", 7);
        Write(Path.Combine("zmodel", "notes.txt"), 3);
        store.Delete(Single());
        store.Delete(Archive());
        Assert.True(File.Exists(Path.Combine(_folder, "other.onnx")));
        Assert.True(File.Exists(Path.Combine(_folder, "zmodel", "notes.txt")));    // the folder is kept because something else is in it
    }

    [Fact]
    public void Every_known_model_has_a_name_and_says_what_it_is_for()
    {
        Assert.Equal(3, ModelCatalog.All.Count);
        foreach (var model in ModelCatalog.All)
        {
            var description = ModelCatalog.Describe(model);
            Assert.False(string.IsNullOrWhiteSpace(description.Title));
            Assert.False(string.IsNullOrWhiteSpace(description.UsedFor));
        }
    }
}
