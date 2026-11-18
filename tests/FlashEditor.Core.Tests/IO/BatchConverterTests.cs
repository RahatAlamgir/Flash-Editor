using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using Xunit;

namespace FlashEditor.Core.Tests.IO;

public class BatchConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flash-batch-" + Guid.NewGuid().ToString("N"));

    public BatchConverterTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { /* temp */ } }

    private string Make(string relative, int w, int h)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var image = new PixelBuffer(w, h);
        image.Fill(new ColorRgba(10, 120, 200));
        ImageEncoder.Save(image, path, ImageFileFormat.Png);
        return path;
    }

    [Fact]
    public void A_picture_is_never_enlarged_and_keeps_its_proportions()
    {
        Assert.Equal((100, 50), BatchConverter.FitSize(200, 100, 100, 0));
        Assert.Equal((200, 100), BatchConverter.FitSize(200, 100, 1000, 1000));
        Assert.Equal((60, 30), BatchConverter.FitSize(200, 100, 100, 30));
        Assert.Equal((200, 100), BatchConverter.FitSize(200, 100, 0, 0));
    }

    [Fact]
    public void Every_picture_of_the_folder_is_converted_and_the_originals_stay()
    {
        string a = Make("in/a.png", 80, 40);
        Make("in/b.png", 20, 20);
        File.WriteAllText(Path.Combine(_root, "in", "notes.txt"), "not a picture");
        var options = new BatchOptions(Path.Combine(_root, "in"), Path.Combine(_root, "out"), ImageFileFormat.Jpeg, 80, MaxWidth: 40);

        var result = BatchConverter.Run(options);
        Assert.Equal(2, result.Converted);
        Assert.Empty(result.Failed);
        Assert.True(File.Exists(a));
        var converted = ImageDecoder.Load(Path.Combine(_root, "out", "a.jpg"));
        Assert.Equal((40, 20), (converted.Width, converted.Height));
        Assert.Equal((20, 20), (ImageDecoder.Load(Path.Combine(_root, "out", "b.jpg")).Width, 20));
    }

    [Fact]
    public void Nothing_is_overwritten_a_number_is_added()
    {
        Make("in/a.png", 10, 10);
        var options = new BatchOptions(Path.Combine(_root, "in"), Path.Combine(_root, "out"), ImageFileFormat.Png);
        BatchConverter.Run(options);
        BatchConverter.Run(options);
        Assert.True(File.Exists(Path.Combine(_root, "out", "a.png")));
        Assert.True(File.Exists(Path.Combine(_root, "out", "a (2).png")));
    }

    [Fact]
    public void The_results_folder_inside_the_input_is_not_converted_again()
    {
        Make("in/a.png", 10, 10);
        var options = new BatchOptions(Path.Combine(_root, "in"), Path.Combine(_root, "in", "converted"), ImageFileFormat.Png, IncludeSubfolders: true);
        Assert.Equal(1, BatchConverter.Run(options).Converted);
        Assert.Single(BatchConverter.FindPictures(options));
    }

    [Fact]
    public void Subfolders_keep_their_layout()
    {
        Make("in/sub/c.png", 10, 10);
        var options = new BatchOptions(Path.Combine(_root, "in"), Path.Combine(_root, "out"), ImageFileFormat.Bmp, IncludeSubfolders: true);
        BatchConverter.Run(options);
        Assert.True(File.Exists(Path.Combine(_root, "out", "sub", "c.bmp")));
    }

    [Fact]
    public void A_broken_picture_is_reported_and_the_others_still_convert()
    {
        Make("in/good.png", 10, 10);
        File.WriteAllText(Path.Combine(_root, "in", "bad.png"), "this is not a png");
        var result = BatchConverter.Run(new BatchOptions(Path.Combine(_root, "in"), Path.Combine(_root, "out"), ImageFileFormat.Png));
        Assert.Equal(1, result.Converted);
        Assert.Single(result.Failed);
    }
}
