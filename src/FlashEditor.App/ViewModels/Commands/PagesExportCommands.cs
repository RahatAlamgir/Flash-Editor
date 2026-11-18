using System.IO;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.IO.Pdf;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Effects;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>File > Export as PDF and Export as Animated GIF: every page of the project becomes a PDF page or a GIF frame.</summary>
public sealed class PagesExportCommands
{
    private readonly MainViewModel _main;

    public PagesExportCommands(MainViewModel main)
    {
        _main = main;
        ExportPdfCommand = new RelayCommand(ExportPdf);
        ExportGifCommand = new RelayCommand(ExportGif);
        BatchConvertCommand = new RelayCommand(BatchConvert);
    }

    public RelayCommand ExportPdfCommand { get; }
    public RelayCommand ExportGifCommand { get; }
    public RelayCommand BatchConvertCommand { get; }

    private DocumentViewModel Doc => _main.Document;

    private List<PixelBuffer> FlattenPages() => Doc.Project.Pages.Select(p => Compositor.Flatten(p.Model)).ToList();

    private string SuggestedName => Path.GetFileNameWithoutExtension(Doc.FilePath ?? Doc.DisplayName);

    /// <summary>File > Batch Convert: converts every picture of a folder (format and size) without opening them.</summary>
    private void BatchConvert()
    {
        var options = _main.Dialogs.ShowBatch();
        if (options == null) return;
        _main.RunAsync("Converting the pictures", async () =>
        {
            var progress = new Progress<(int Done, int Total)>(p => _main.SetStatus($"Converting pictures... {p.Done} of {p.Total}"));
            var result = await Task.Run(() => BatchConverter.Run(options, progress));
            _main.SetStatus($"Converted {result.Converted} picture{(result.Converted == 1 ? "" : "s")} into {options.OutputFolder}"
                + (result.Failed.Count > 0 ? $" ({result.Failed.Count} could not be converted)" : ""));
            if (result.Failed.Count > 0)
                _main.Dialogs.ShowInfo("Batch Convert", "These pictures could not be converted:\n\n" + string.Join("\n", result.Failed.Take(12))
                    + (result.Failed.Count > 12 ? $"\n... and {result.Failed.Count - 12} more" : ""));
        });
    }

    private void ExportPdf()
    {
        var quality = new EffectParameter("quality", "Picture quality", 40, 100, 90, 1, "%");
        var dpi = new EffectParameter("dpi", "Resolution", 72, 600, 96, 1, " dpi");
        var values = _main.Dialogs.ShowParameters("Export as PDF", new[] { quality, dpi }, new EffectValues(new[] { quality, dpi }), null,
            "Every page of this project becomes a PDF page. 96 dpi keeps the on-screen size; use 300 dpi for pictures made for print (the page is then smaller on paper but sharper).");
        if (values == null) return;
        var path = _main.Dialogs.ShowSaveFile("Save as PDF", "PDF document|*.pdf", ".pdf", SuggestedName);
        if (path == null) return;

        var pages = FlattenPages();
        int q = (int)values["quality"];
        double d = values["dpi"];
        var matte = _main.Colors.Background;
        _main.RunAsync("Saving the PDF", async () =>
        {
            await Task.Run(() => PdfWriter.Save(pages, path, q, d, matte));
            _main.SetStatus($"Saved {Path.GetFileName(path)}: {pages.Count} page{(pages.Count == 1 ? "" : "s")}.");
        });
    }

    private void ExportGif()
    {
        var delay = new EffectParameter("delay", "Time per page", 100, 10000, 1000, 50, " ms");
        var loop = EffectParameter.Toggle("loop", "Repeat forever", true);
        var values = _main.Dialogs.ShowParameters("Export as animated GIF", new[] { delay, loop }, new EffectValues(new[] { delay, loop }), null,
            "Every page of this project becomes one frame. GIF has only 256 colors per frame and no soft transparency, so photos look best as PDF or video.");
        if (values == null) return;
        var path = _main.Dialogs.ShowSaveFile("Save as animated GIF", "GIF animation|*.gif", ".gif", SuggestedName);
        if (path == null) return;

        var pages = FlattenPages();
        int ms = (int)values["delay"];
        bool repeat = values["loop"] >= 0.5;
        _main.RunAsync("Saving the GIF", async () =>
        {
            await Task.Run(() => File.WriteAllBytes(path, GifEncoder.EncodeAnimation(pages, ms, repeat)));
            _main.SetStatus(pages.Count == 1
                ? $"Saved {Path.GetFileName(path)} (one page, so it does not move: add pages with Ctrl+Alt+N for more frames)."
                : $"Saved {Path.GetFileName(path)}: {pages.Count} frames, {ms} ms each.");
        });
    }
}
