using FlashEditor.AI.Effects;
using FlashEditor.AI.Models;
using FlashEditor.AI.Segmentation;
using FlashEditor.App.Services;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Effects;
using FlashEditor.Core.Effects.Adjustments;
using FlashEditor.Core.Effects.Background;
using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.History;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>
/// Adjust and Filter menus. Every effect runs the same way: effects with settings open a dialog with
/// live preview; effects without settings are applied immediately. Each becomes one undo step.
/// </summary>
public sealed class EffectCommands
{
    private readonly MainViewModel _main;

    public EffectCommands(MainViewModel main)
    {
        _main = main;
        BrightnessContrastCommand = Create(() => new BrightnessContrastEffect());
        HueSaturationCommand = Create(() => new HueSaturationEffect());
        GrayscaleCommand = Create(() => new GrayscaleEffect());
        InvertCommand = Create(() => new InvertEffect());
        SepiaCommand = Create(() => new SepiaEffect());
        AutoLevelsCommand = Create(() => new AutoContrastEffect(),
            "Stretches the tones so the darkest parts become black and the brightest white. Good first fix for dull or hazy photos.");
        ExposureCommand = Create(() => new ExposureEffect());
        TemperatureCommand = Create(() => new TemperatureEffect());
        VibranceCommand = Create(() => new VibranceEffect(),
            "Boosts dull colors more than strong ones (gentler than Saturation).");
        PosterizeCommand = Create(() => new PosterizeEffect());
        ThresholdCommand = Create(() => new ThresholdEffect());
        BlurCommand = Create(() => new GaussianBlurEffect());
        SharpenCommand = Create(() => new SharpenEffect());
        NoiseCommand = Create(() => new NoiseEffect());
        PixelateCommand = Create(() => new PixelateEffect());
        MotionBlurCommand = Create(() => new MotionBlurEffect());
        EnhanceCommand = Create(() => new EnhanceEffect(),
            "Makes a soft or slightly blurry picture cleaner: removes a little noise, sharpens details and adds clarity. " +
            "For small pictures, try Image > Upscale 2x first. It cannot bring back detail that is completely gone.");
        ReduceNoiseCommand = Create(() => new ReduceNoiseEffect(),
            "Removes grain and speckles. Radius 1 is usually enough; higher values look painterly.");
        VignetteCommand = Create(() => new VignetteEffect());
        EmbossCommand = Create(() => new EmbossEffect());
        FindEdgesCommand = Create(() => new FindEdgesEffect());
        RemoveBackgroundCommand = new RelayCommand(RemoveBackgroundWithAi, () => _main.Document.ActiveLayer != null);
        ShowAiModelsCommand = new RelayCommand(ShowAiModels);
        RemoveBackgroundByColorCommand = Create(() => new BackgroundRemovalEffect(),
            "Removes the background color found along the image edges. Works best on plain or studio " +
            "backgrounds. Raise Tolerance to remove more, lower it if parts of the subject disappear.");
    }

    // Adjustments
    public RelayCommand BrightnessContrastCommand { get; }
    public RelayCommand HueSaturationCommand { get; }
    public RelayCommand GrayscaleCommand { get; }
    public RelayCommand InvertCommand { get; }
    public RelayCommand SepiaCommand { get; }
    public RelayCommand AutoLevelsCommand { get; }
    public RelayCommand ExposureCommand { get; }
    public RelayCommand TemperatureCommand { get; }
    public RelayCommand VibranceCommand { get; }
    public RelayCommand PosterizeCommand { get; }
    public RelayCommand ThresholdCommand { get; }

    // Filters
    public RelayCommand BlurCommand { get; }
    public RelayCommand SharpenCommand { get; }
    public RelayCommand NoiseCommand { get; }
    public RelayCommand PixelateCommand { get; }
    public RelayCommand MotionBlurCommand { get; }
    public RelayCommand EnhanceCommand { get; }
    public RelayCommand ReduceNoiseCommand { get; }
    public RelayCommand VignetteCommand { get; }
    public RelayCommand EmbossCommand { get; }
    public RelayCommand FindEdgesCommand { get; }

    /// <summary>AI removal (works on any photo). Downloads the model once if needed.</summary>
    public RelayCommand RemoveBackgroundCommand { get; }

    /// <summary>Color-based removal (no AI; for plain backgrounds, logos, graphics).</summary>
    public RelayCommand RemoveBackgroundByColorCommand { get; }

    private readonly ModelStore _models = new();
    private SubjectSegmenter? _segmenter;

    private const string AiDescription =
        "AI finds the main subject (person, animal, product...) and removes everything else. The first run takes " +
        "a few seconds. Edge sharpness: higher = crisper cut. Shrink / grow: negative removes more around the edge, " +
        "positive keeps more.";

    /// <summary>Change the color of the selection (e.g. a dress) with a color graph, keeping its shading.</summary>
    public RelayCommand RecolorCommand => _recolorCommand ??= new RelayCommand(Recolor, () => _main.Document.ActiveLayer != null);
    private RelayCommand? _recolorCommand;

    private void Recolor()
    {
        var doc = _main.Document.Model;
        if (doc.Selection == null && !_main.Dialogs.Confirm("Recolor",
                "Nothing is selected, so the whole layer would be recolored.\n\n" +
                "Tip: first select the part to change (e.g. the dress) with the Magic Wand (W), Lasso (L) or " +
                "Select > Select Subject (AI). Shift adds to a selection.\n\nRecolor the whole layer anyway?"))
            return;
        if (!_main.TryGetEditableLayer(out var layer)) return;
        _main.Tools.ResetActiveTool();

        var (average, lightness, saturation) = RecolorEffect.Measure(layer.Pixels, doc.Selection);
        var effect = new RecolorEffect(lightness, saturation);
        var session = new EffectPreviewSession(layer, effect, doc.Selection);
        var initial = effect.CreateDefaultValues();
        RecolorEffect.SetTarget(initial, average);

        var values = _main.Dialogs.ShowRecolor(average, initial, session.Update);
        if (values == null)
        {
            session.Cancel();
            return;
        }
        _main.RunAsync("Recolor", async () =>
        {
            _main.Push(await session.CommitAsync(values));
            _main.SetStatus("Recolored. Ctrl+Z to undo.");
        });
    }

    /// <summary>Selects the main subject of the visible picture using the AI model.</summary>
    public RelayCommand SelectSubjectCommand => _selectSubjectCommand ??= new RelayCommand(SelectSubject);
    private RelayCommand? _selectSubjectCommand;

    private void SelectSubject() => WithAiModel("Select Subject (AI)", segmenter =>
    {
        var doc = _main.Document.Model;
        var flat = Core.Painting.Compositor.Flatten(doc);
        _main.SetStatus("AI is finding the subject...");
        _main.RunAsync("Select Subject", async () =>
        {
            var mask = await Task.Run(() => segmenter.PredictMask(flat));
            var bytes = new byte[mask.Length];
            for (int i = 0; i < mask.Length; i++) bytes[i] = mask[i] >= 0.5f ? (byte)255 : (byte)0;
            doc.Selection = Core.Selections.Selection.FromMask(bytes, doc.Width, doc.Height);
            _main.SetStatus(doc.Selection == null
                ? "No clear subject found."
                : "Subject selected. Copy (Ctrl+C), delete, or apply an adjustment to just the subject.");
        });
    });

    private void RemoveBackgroundWithAi() => WithAiModel("Remove Background (AI)", segmenter =>
    {
        _main.SetStatus("AI is finding the subject (a few seconds the first time)...");
        Run(new AiBackgroundRemovalEffect(segmenter), AiDescription);
    });

    /// <summary>The AI subject finder when the model is installed, else null (no download prompt).</summary>
    public Func<Core.Imaging.PixelBuffer, CancellationToken, float[]>? SubjectMaskIfInstalled
    {
        get
        {
            var model = ModelInfo.IsNetGeneralUse;
            if (!_models.IsInstalled(model)) return null;
            _segmenter ??= new SubjectSegmenter(model, _models.PathOf(model));
            return _segmenter.PredictMask;
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the AI model, downloading it first (after asking) when missing.
    /// </summary>
    public void WithAiModel(string title, Action<SubjectSegmenter> action)
    {
        var model = ModelInfo.IsNetGeneralUse;
        EnsureModel(model, title, () =>
        {
            _segmenter ??= new SubjectSegmenter(model, _models.PathOf(model));
            action(_segmenter);
        });
    }

    /// <summary>The place AI models are found and downloaded to.</summary>
    public ModelStore Models => _models;

    /// <summary>Settings > AI Models: see, download and delete the AI models.</summary>
    public RelayCommand ShowAiModelsCommand { get; }

    private void ShowAiModels()
    {
        var viewModel = new AiModelsViewModel(_models, ReleaseModel, _main.Dialogs.Confirm);
        _main.Dialogs.ShowAiModels(viewModel);
    }

    /// <summary>Lets go of a model that is loaded in memory, so that its file can be deleted. It loads again when it is next used.</summary>
    public void ReleaseModel(ModelInfo model)
    {
        if (model == ModelInfo.IsNetGeneralUse)
        {
            _segmenter?.Dispose();
            _segmenter = null;
        }
        else if (model == ModelInfo.RealEsrganX4Plus) _main.Upscale.Release();
        else if (model == ModelInfo.LamaInpainting) _main.Inpaint.Release();
    }

    /// <summary>
    /// Runs <paramref name="ready"/> once <paramref name="model"/> is on this PC, downloading it first
    /// (after asking) when it is missing.
    /// </summary>
    public void EnsureModel(ModelInfo model, string title, Action ready)
    {
        if (_models.IsInstalled(model))
        {
            ready();
            return;
        }

        bool download = _main.Dialogs.Confirm(title,
            $"This AI feature needs a one-time download of the {model.DisplayName} ({model.SizeText}).\n\n" +
            $"It is saved on this PC in:\n{_models.Folder}\nand afterwards works without internet.\n\nDownload it now?");
        if (!download) return;

        _main.RunAsync("Downloading the AI model", async () =>
        {
            var progress = new Progress<double>(p => _main.SetStatus($"Downloading the AI model... {p:P0}"));
            await _models.DownloadAsync(model, progress);
            _main.SetStatus("AI model downloaded.");
            System.Windows.Input.Mouse.OverrideCursor = null; // a dialog may open next
            ready();
        });
    }

    private RelayCommand Create(Func<ImageEffect> factory, string? description = null)
        => new(() => Run(factory(), description), () => _main.Document.ActiveLayer != null);

    private void Run(ImageEffect effect, string? description)
    {
        if (!_main.TryGetEditableLayer(out var layer)) return;
        bool isRemoval = effect is BackgroundRemovalEffect or AiBackgroundRemovalEffect;
        if (isRemoval && !CheckBackgroundRemoval(layer)) return;
        // Count before the dialog: its live preview already changes the layer.
        long opaqueBefore = isRemoval ? CountVisible(layer.Pixels) : 0;
        _main.Tools.ResetActiveTool();
        var session = new EffectPreviewSession(layer, effect, _main.Document.Model.Selection);

        EffectValues values;
        if (effect.HasParameters)
        {
            var chosen = _main.Dialogs.ShowParameters(effect.Name, effect.Parameters,
                effect.CreateDefaultValues(), session.Update, description);
            if (chosen == null)
            {
                session.Cancel();
                return;
            }
            values = chosen;
        }
        else
        {
            values = effect.CreateDefaultValues();
        }

        _main.RunAsync(effect.Name, async () =>
        {
            var step = await session.CommitAsync(values);
            _main.SetStatus($"Applied {effect.Name}");
            if (!isRemoval)
            {
                _main.Push(step);
                return;
            }

            // The cut-out becomes an editable picture (it can be moved, resized, rotated and given a shadow like any
            // object) that remembers the picture as it was, so the Refine brush can always paint parts back.
            long opaqueAfter = CountVisible(layer.Pixels);
            var cutOut = MakeCutOutObject(layer, session.Original);
            _main.Push(cutOut == null ? step : new CompositeAction(effect.Name, new[] { step, cutOut }));
            if (cutOut == null && layer.RestoreSource == null)
                layer.RestoreSource = session.Original; // rare layers that cannot become an object keep the older way
            ReportBackgroundRemoval(effect, opaqueBefore, opaqueAfter);
        });
    }

    /// <summary>
    /// Turns a layer whose background was just removed into a picture object: the cut-out and the original (both cropped to the
    /// area the original covered) go into one <see cref="ImageObject"/>. Returns the undo step, or null when the layer is left as it is.
    /// </summary>
    private static IHistoryAction? MakeCutOutObject(Core.Documents.Layer layer, Core.Imaging.PixelBuffer original)
    {
        if (layer.HasHiddenPixels || original.Width != layer.Width || original.Height != layer.Height) return null;

        // Everything the original had, so removed parts can be brought back.
        var area = original.VisibleBounds().Union(layer.Pixels.VisibleBounds());
        if (area.IsEmpty) return null;

        var before = layer.SaveState();
        layer.SetObject(new ImageObject(new ObjectBox(area.X, area.Y, area.Width, area.Height),
            layer.Pixels.Crop(area), layer.Name, Original: original.Crop(area)));
        return new LayerContentAction("Cut-out", layer, before, layer.SaveState());
    }

    /// <summary>
    /// Checks before Remove Background whether it can do anything at all, and explains why not.
    /// Returns false when the effect should not run.
    /// </summary>
    private bool CheckBackgroundRemoval(Core.Documents.Layer layer)
    {
        if (CountVisible(layer.Pixels) == 0)
        {
            _main.Dialogs.ShowInfo("Remove Background",
                $"The selected layer \"{layer.Name}\" is empty, so there is nothing to remove.\n\n" +
                "Click the layer that contains your picture in the Layers panel, then try again.");
            return false;
        }
        return true;
    }

    private void ReportBackgroundRemoval(ImageEffect effect, long before, long after)
    {
        double share = before == 0 ? 0 : (before - after) / (double)before;
        _main.SetStatus($"Remove Background: {share:P0} of the layer became transparent. Missed a spot? Use the Refine brush (R): Restore paints parts back, Remove takes more away.");
        if (share >= 0.01) return;

        string reason = _main.Document.Model.Selection != null
            ? "There is an active selection, so only the selected area was processed. Press Ctrl+Shift+D (or Esc) to deselect and try again.\n\n"
            : "";
        if (effect is AiBackgroundRemovalEffect)
        {
            _main.Dialogs.ShowInfo("Remove Background (AI)",
                "Almost nothing was removed.\n\n" + reason +
                "The AI treated the whole picture as the subject. This happens when there is no clear main subject " +
                "(e.g. a landscape) or the picture is already cut out. Try a negative \"Shrink / grow edge\" value.");
            return;
        }
        _main.Dialogs.ShowInfo("Remove Background",
            "Almost nothing was removed.\n\n" + reason +
            "This tool removes the background color found along the edges of the picture. It works on plain " +
            "or simple backgrounds (studio, wall, sky, product photos). If the edges of your picture are very busy " +
            "(a room, trees, a crowd), try a higher Tolerance, or remove parts by hand with the Lasso (L) and Delete, " +
            "or with the Eraser (E). For photos, use the AI version: Image > Remove Background (AI).");
    }

    private static long CountVisible(Core.Imaging.PixelBuffer pixels)
    {
        long count = 0;
        var data = pixels.Data;
        for (int i = 3; i < data.Length; i += 4)
            if (data[i] > 127) count++;
        return count;
    }
}
