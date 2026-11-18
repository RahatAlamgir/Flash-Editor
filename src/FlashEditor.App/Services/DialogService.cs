using System.Windows;
using FlashEditor.App.Views.Dialogs;
using FlashEditor.Core.Effects;
using FlashEditor.Core.Imaging;
using System.IO;
using FlashEditor.Core.IO;
using FlashEditor.Core.IO.Project;
using Microsoft.Win32;

namespace FlashEditor.App.Services;

/// <summary>WPF implementation of <see cref="IDialogService"/>.</summary>
public sealed class DialogService : IDialogService
{
    /// <summary>
    /// The window a dialog belongs to: the window the user is working in (so a dialog opened from the Project Manager belongs to the manager),
    /// else the editor if it is shown. Null when no window is shown yet (the program is just starting).
    /// </summary>
    private static Window? Owner
    {
        get
        {
            var app = Application.Current;
            if (app == null) return null;
            // The window in front: the active one, else the newest window that is shown (a dialog opened from the Project Manager belongs to it).
            var shown = app.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
            return shown.FirstOrDefault(w => w.IsActive) ?? shown.LastOrDefault();
        }
    }

    private static bool? ShowFile(Microsoft.Win32.CommonDialog dialog) => Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog();

    /// <summary>A message in the program's own dark look (not the white Windows message box).</summary>
    private static MessageBoxResult Message(string text, string caption, MessageBoxButton buttons, MessageBoxImage icon, string? yesText = null, string? noText = null)
        => MessageDialog.Show(Owner, text, caption, buttons, icon, yesText, noText);

    public string? ShowOpenImage(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = ImageFileFormats.OpenFilter };
        return ShowFile(dialog) == true ? dialog.FileName : null;
    }

    public string? ShowOpenFile(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = ImageFileFormats.OpenAnyFilter };
        return ShowFile(dialog) == true ? dialog.FileName : null;
    }

    public string? ShowSaveProject(string suggestedName, string? folder)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Project",
            Filter = ProjectFile.Filter,
            DefaultExt = ProjectFile.Extension,
            FileName = suggestedName,
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (folder != null && Directory.Exists(folder)) dialog.InitialDirectory = folder;
        return ShowFile(dialog) == true ? dialog.FileName : null;
    }

    public string? ShowSaveFile(string title, string filter, string defaultExtension, string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            FileName = suggestedName,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return ShowFile(dialog) == true ? dialog.FileName : null;
    }

    public FlashEditor.Core.IO.BatchOptions? ShowBatch()
    {
        var dialog = new BatchDialog { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public void ShowBrandKit(PixelBuffer? picture)
    {
        new BrandKitDialog(picture, (initial, title) => PickColor(initial, title)) { Owner = Owner }.ShowDialog();
    }

    public string? ShowSaveSvg(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save as SVG",
            Filter = "SVG vector picture|*.svg",
            DefaultExt = ".svg",
            FileName = suggestedName,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return ShowFile(dialog) == true ? dialog.FileName : null;
    }

    public string? ShowSavePsd(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save as Photoshop file",
            Filter = "Photoshop file|*.psd",
            DefaultExt = ".psd",
            FileName = suggestedName,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return ShowFile(dialog) == true ? dialog.FileName : null;
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
        if (ShowFile(dialog) != true) return null;

        // The typed extension wins; otherwise use the chosen filter.
        var chosen = ImageFileFormats.FromPath(dialog.FileName) ?? ImageFileFormats.FromFilterIndex(dialog.FilterIndex);
        var path = ImageFileFormats.FromPath(dialog.FileName) == null
            ? dialog.FileName + ImageFileFormats.Extension(chosen)
            : dialog.FileName;
        return new SaveTarget(path, chosen);
    }

    public SaveChangesAnswer AskSaveChanges(string documentName)
    {
        var result = Message($"Save changes to \"{documentName}\" before closing it?",
            "Flash Editor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, yesText: "Save", noText: "Don't save");
        return result switch
        {
            MessageBoxResult.Yes => SaveChangesAnswer.Save,
            MessageBoxResult.No => SaveChangesAnswer.Discard,
            _ => SaveChangesAnswer.Cancel,
        };
    }

    public void ShowError(string message)
        => Message(message, "Flash Editor", MessageBoxButton.OK, MessageBoxImage.Error);

    public void ShowInfo(string title, string message)
        => Message(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string title, string message)
        => Message(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public NewImageSettings? ShowNewImage(ColorRgba backgroundColor)
    {
        var dialog = new NewImageDialog(backgroundColor) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public NewImageSettings? ShowNewProject(ColorRgba backgroundColor)
    {
        var dialog = new NewImageDialog(backgroundColor, askName: true) { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public ProjectManagerResult? ShowProjectManager(bool atStartup)
    {
        var window = new ProjectManagerWindow(ProjectLibrary.Default, this, atStartup) { Owner = atStartup ? null : Owner };
        return window.ShowDialog() == true ? window.Result : null;
    }

    public void ShowAiModels(FlashEditor.App.ViewModels.AiModelsViewModel viewModel)
    {
        new AiModelsWindow(viewModel) { Owner = Owner }.ShowDialog();
    }

    public void ShowSettings()
    {
        new SettingsDialog { Owner = Owner }.ShowDialog();
    }

    public ExportForResult? ShowExportFor(PixelBuffer flattenedImage, int pageCount, ImageFileFormat format, int quality)
    {
        var dialog = new ExportForDialog(flattenedImage, pageCount, format, quality) { Owner = Owner };
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
