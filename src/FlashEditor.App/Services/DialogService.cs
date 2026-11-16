using System.Windows;
using FlashEditor.App.Views.Dialogs;
using FlashEditor.Core.Effects;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using Microsoft.Win32;

namespace FlashEditor.App.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/>.</summary>
public sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow;

    public string? ShowOpenImage(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = ImageFileFormats.OpenFilter };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public SaveTarget? ShowSaveImage(string suggestedName, ImageFileFormat? format)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Image As",
            Filter = ImageFileFormats.SaveFilter,
            FilterIndex = ImageFileFormats.ToFilterIndex(format ?? ImageFileFormat.Png),
            FileName = suggestedName,
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(Owner) != true) return null;

        // The typed extension wins; otherwise use the chosen filter.
        var chosen = ImageFileFormats.FromPath(dialog.FileName) ?? ImageFileFormats.FromFilterIndex(dialog.FilterIndex);
        var path = ImageFileFormats.FromPath(dialog.FileName) == null
            ? dialog.FileName + ImageFileFormats.Extension(chosen)
            : dialog.FileName;
        return new SaveTarget(path, chosen);
    }

    public SaveChangesAnswer AskSaveChanges(string documentName)
    {
        var result = MessageBox.Show(Owner!, $"Save changes to \"{documentName}\" before closing it?",
            "Flash Editor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => SaveChangesAnswer.Save,
            MessageBoxResult.No => SaveChangesAnswer.Discard,
            _ => SaveChangesAnswer.Cancel,
        };
    }

    public void ShowError(string message)
        => MessageBox.Show(Owner!, message, "Flash Editor", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message)
        => MessageBox.Show(Owner!, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string title, string message)
        => MessageBox.Show(Owner!, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public NewImageSettings? ShowNewImage(ColorRgba backgroundColor)
    {
        var dialog = new NewImageDialog(backgroundColor) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public ImageSizeSettings? ShowImageSize(int width, int height)
    {
        var dialog = new ImageSizeDialog(width, height) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public ExportSettings? ShowExport(PixelBuffer flattenedImage, ImageFileFormat format, int quality, ColorRgba matte)
    {
        var dialog = new ExportDialog(flattenedImage, format, quality, matte) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public EffectValues? ShowRecolor(ColorRgba original, EffectValues initial, Action<EffectValues>? onChanged)
    {
        var dialog = new RecolorDialog(original, initial, onChanged) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public bool RunWithProgress(string title, string message,
        Func<IProgress<(double Fraction, string? Detail)>, CancellationToken, Task> work)
    {
        using var cts = new CancellationTokenSource();
        var dialog = new ProgressDialog(title, message) { Owner = Owner };
        dialog.CancelRequested += cts.Cancel;
        var progress = new Progress<(double Fraction, string? Detail)>(p => dialog.Report(p.Fraction, p.Detail));
        Exception? error = null;
        bool completed = false;
        dialog.Loaded += async (_, _) =>
        {
            try
            {
                await work(progress, cts.Token);
                completed = !cts.IsCancellationRequested;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { error = ex; }
            dialog.Finish();
        };
        dialog.ShowDialog();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(error);
        return completed;
    }

    public SplitSheetResult? ShowSplitSheet(PixelBuffer sheet, string baseName, string? folder, byte[]? selectionMask)
    {
        var dialog = new SplitSheetDialog(sheet, baseName, folder, selectionMask) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public CanvasSizeSettings? ShowCanvasSize(int width, int height)
    {
        var dialog = new CanvasSizeDialog(width, height) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public string? ShowTextInput(string title, string prompt, string initialText)
    {
        var dialog = new TextInputDialog(title, prompt, initialText) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Text : null;
    }

    public ColorRgba? PickColor(ColorRgba initial, string title)
    {
        var dialog = new ColorPickerDialog(initial, title) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
    }

    public EffectValues? ShowParameters(string title, IReadOnlyList<EffectParameter> parameters,
        EffectValues initial, Action<EffectValues>? onChanged, string? description = null)
    {
        var dialog = new ParametersDialog(title, parameters, initial, onChanged, description) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Values : null;
    }
}
