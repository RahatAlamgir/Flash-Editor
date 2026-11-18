using FlashEditor.Core.Effects;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.Transforms;

namespace FlashEditor.App.Services;

public sealed record NewImageSettings(int Width, int Height, ColorRgba Background, string? Template = null, string? Name = null);

/// <summary>What the Project Manager asks the editor to do.</summary>
public enum ProjectManagerAction { Open, New }

/// <param name="Path">The project to open (for <see cref="ProjectManagerAction.Open"/>).</param>
/// <param name="New">The size, template and name of the project to create (for <see cref="ProjectManagerAction.New"/>).</param>
public sealed record ProjectManagerResult(ProjectManagerAction Action, string? Path = null, NewImageSettings? New = null);
public sealed record ImageSizeSettings(int Width, int Height, ResampleMode Mode);
/// <param name="AnchorX">0 = left, 0.5 = center, 1 = right (where the old picture stays).</param>
/// <param name="AnchorY">0 = top, 0.5 = middle, 1 = bottom.</param>
public sealed record CanvasSizeSettings(int Width, int Height, double AnchorX, double AnchorY);
/// <summary>What the user chose in the Split Asset Sheet dialog.</summary>
public sealed record SplitSheetResult(IReadOnlyList<FlashEditor.Core.Sheets.SheetPiece> Pieces, bool AsLayers, bool HideOriginal,
    string? Folder, string BaseName);
public sealed record SaveTarget(string Path, ImageFileFormat Format);

/// <summary>What the user chose in the Export For dialog.</summary>
public sealed record ExportForResult(ExportPreset Preset, ExportFit Fit, ImageFileFormat Format, int Quality, bool Transparent, bool AllPages);
public enum SaveChangesAnswer { Save, Discard, Cancel }

/// <summary>
/// Every window/dialog the view models need. Keeping this behind an interface means view models
/// never create windows themselves (and could be tested without a UI).
/// </summary>
public interface IDialogService
{
    string? ShowOpenImage(string title);

    /// <summary>File > Open: a picture or a saved project.</summary>
    string? ShowOpenFile(string title);

    /// <summary>File > Save As: where to save the project (always a .flash file). Null when cancelled.</summary>
    string? ShowSaveProject(string suggestedName, string? folder);
    SaveTarget? ShowSaveImage(string suggestedName, ImageFileFormat? format);

    /// <summary>File > Export as Photoshop file: where to save the .psd. Null when cancelled.</summary>
    string? ShowSavePsd(string suggestedName);

    /// <summary>File > Export as SVG: where to save the .svg. Null when cancelled.</summary>
    string? ShowSaveSvg(string suggestedName);

    /// <summary>Settings > Brand Kit. <paramref name="picture"/> is the picture being edited (colors can be taken from it), or null.</summary>
    void ShowBrandKit(PixelBuffer? picture);

    /// <summary>File > Batch Convert: which folder, format and size. Null when cancelled.</summary>
    FlashEditor.Core.IO.BatchOptions? ShowBatch();

    /// <summary>A plain "save as" box for one kind of file (title, filter like "PDF document|*.pdf", extension with dot). Null when cancelled.</summary>
    string? ShowSaveFile(string title, string filter, string defaultExtension, string suggestedName);
    SaveChangesAnswer AskSaveChanges(string documentName);
    void ShowError(string message);
    void ShowInfo(string title, string message);

    /// <summary>Yes / No question. Returns true for Yes.</summary>
    bool Confirm(string title, string message);

    NewImageSettings? ShowNewImage(ColorRgba backgroundColor);

    /// <summary>The new project dialog: like New Image, plus a name for the project.</summary>
    NewImageSettings? ShowNewProject(ColorRgba backgroundColor);

    /// <summary>
    /// The Project Manager: all projects, to open or create one. Returns what the user chose, or null when they closed it
    /// (at start-up that means "quit").
    /// </summary>
    ProjectManagerResult? ShowProjectManager(bool atStartup);
    /// <summary>The AI Models window: see, download and delete the AI models.</summary>
    void ShowAiModels(FlashEditor.App.ViewModels.AiModelsViewModel viewModel);

    /// <summary>The Settings window (how many undo steps a project keeps...). It saves the choices itself.</summary>
    void ShowSettings();

    ImageSizeSettings? ShowImageSize(int width, int height);

    /// <summary>The Export For dialog: pick a ready-made size (a social post, a thumbnail, a print...) and how the picture fits it.</summary>
    ExportForResult? ShowExportFor(PixelBuffer flattenedImage, int pageCount, ImageFileFormat format, int quality);

    /// <summary>Export options (format, quality, output size) for <paramref name="flattenedImage"/>.</summary>
    ExportSettings? ShowExport(PixelBuffer flattenedImage, ImageFileFormat format, int quality, ColorRgba matte);

    /// <summary>
    /// Color graph for recoloring: <paramref name="onChanged"/> gets the values while the user drags
    /// (for live preview). Returns the final values, or null when cancelled.
    /// </summary>
    EffectValues? ShowRecolor(ColorRgba original, EffectValues initial, Action<EffectValues>? onChanged);

    /// <summary>
    /// Runs a long job while showing a progress window with Cancel (the picture cannot be edited
    /// meanwhile). The job reports 0..1 plus optional detail text. Returns false when cancelled;
    /// errors from the job are passed on.
    /// </summary>
    bool RunWithProgress(string title, string message, Func<IProgress<(double Fraction, string? Detail)>, CancellationToken, Task> work);

    /// <summary>Find the items on an asset sheet and choose what to do with them.</summary>
    SplitSheetResult? ShowSplitSheet(PixelBuffer sheet, string baseName, string? folder, byte[]? selectionMask);

    /// <summary>New canvas size and where the existing picture sits on it.</summary>
    CanvasSizeSettings? ShowCanvasSize(int width, int height);
    string? ShowTextInput(string title, string prompt, string initialText);
    ColorRgba? PickColor(ColorRgba initial, string title);

    /// <summary>
    /// Shows sliders for the given parameters. <paramref name="onChanged"/> is called (debounced) while
    /// the user drags, for live preview. Returns the final values, or null when cancelled.
    /// </summary>
    EffectValues? ShowParameters(string title, IReadOnlyList<EffectParameter> parameters,
        EffectValues initial, Action<EffectValues>? onChanged, string? description = null);
}
