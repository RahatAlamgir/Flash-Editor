using FlashEditor.AI.Models;
using FlashEditor.AI.Upscaling;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>
/// AI Upscale / Clean Up (Real-ESRGAN): makes a picture sharper and cleaner at the same size, or
/// 2x / 3x / 4x larger with real-looking detail. Downloads the model once (after asking).
/// </summary>
public sealed class UpscaleCommands
{
    private readonly MainViewModel _main;
    private AiUpscaler? _upscaler;
    private double _pendingScale = 1; // the size chosen for the run being prepared

    public UpscaleCommands(MainViewModel main)
    {
        _main = main;
        AiUpscaleCommand = new RelayCommand(Start, () => _main.Document.ActiveLayer != null);
    }

    public RelayCommand AiUpscaleCommand { get; }

    private Document Doc => _main.Document.Model;

    private void Start()
    {
        var model = ModelInfo.RealEsrganX4Plus;
        _main.Effects.EnsureModel(model, "AI Upscale / Clean Up", () =>
        {
            _upscaler ??= new AiUpscaler(model, _main.Effects.Models.PathOf(model));
            Ask(_upscaler);
        });
    }

    private void Ask(AiUpscaler upscaler)
    {
        var scale = new EffectParameter("scale", "Size (1 = same size)", 1, 4, 1, 1, "x");
        var strength = new EffectParameter("strength", "Strength", 0, 100, 100, 1, "%");
        var parameters = new[] { scale, strength };
        int tiles = upscaler.TileCount(Doc.Width, Doc.Height);
        // Roughly 0.1 s per tile on a graphics card, 2 s on the processor.
        string time = $"about {Math.Max(1, Math.Ceiling(tiles * 0.12))} s with a graphics card, up to {Math.Max(1, Math.Ceiling(tiles * 2 / 60.0))} min without one";
        var values = _main.Dialogs.ShowParameters("AI Upscale / Clean Up", parameters, new EffectValues(parameters), null,
            "AI makes the picture sharper and cleaner (removes blur, blockiness and noise).\n" +
            "Size 1 = same size, just cleaner. 2, 3 or 4 = that many times larger, with sharp new detail.\n" +
            "Strength below 100% mixes in the original look.\n\n" +
            $"This picture needs {tiles} tiles: {time} on this PC.");
        if (values == null) return;
        Run(upscaler, values["scale"], values["strength"] / 100);
    }

    /// <summary>One piece of work: a painted layer, or the source pixels of a placed / pasted picture.</summary>
    private sealed record Job(Layer Layer, PixelBuffer Source, double Factor, ImageObject? Image);

    private void Run(AiUpscaler upscaler, double scale, double strength)
    {
        int w = (int)Math.Round(Doc.Width * scale), h = (int)Math.Round(Doc.Height * scale);
        if (w > ImageDecoder.MaxDimension || h > ImageDecoder.MaxDimension)
        {
            _main.Dialogs.ShowError($"The result would be {w} x {h} px, larger than the {ImageDecoder.MaxDimension} px limit. Choose a smaller size.");
            return;
        }
        _main.Tools.ResetActiveTool();
        _pendingScale = scale;

        // Same size: only the selected layer. Larger: every painted layer and every placed picture.
        // Shapes and text are drawn from vectors, so they are already sharp at any size.
        IEnumerable<Layer> candidates;
        if (scale == 1)
        {
            if (!_main.TryGetVisibleLayer(out var active) || _main.IsLockedWithMessage(active)) return;
            candidates = new[] { active };
        }
        else
        {
            candidates = Doc.Layers.Where(l => l.IsVisible);
        }
        var jobs = candidates.Select(MakeJob).OfType<Job>().ToList();
        if (jobs.Count == 0)
        {
            _main.Dialogs.ShowInfo("AI Upscale / Clean Up",
                scale == 1
                    ? "The selected layer has nothing the AI can improve (it is empty, or a shape / text, which are always sharp). Select the layer with your picture in the Layers panel."
                    : "There is no picture to improve here (only shapes / text, which are always sharp).");
            return;
        }

        // Results are kept per job (not per pixel buffer: those can be swapped by other edits).
        var results = new Dictionary<Job, PixelBuffer>();
        string device = "";
        bool finished;
        try
        {
            finished = _main.Dialogs.RunWithProgress("AI Upscale / Clean Up",
                "AI is working. You can cancel at any time; nothing is changed until it finishes.",
                async (progress, cancel) =>
                {
                    // Loading the model (and, the first time, preparing it for the GPU) can take a few seconds.
                    progress.Report((0, "Loading the AI model..."));
                    device = await Task.Run(() => upscaler.DeviceName, cancel);
                    for (int i = 0; i < jobs.Count; i++)
                    {
                        var job = jobs[i];
                        string detail = (jobs.Count > 1 ? $"Layer {i + 1} of {jobs.Count}, " : "") + $"on {device}";
                        int index = i;
                        var tileProgress = new Progress<double>(p => ((IProgress<(double, string?)>)progress).Report(((index + p) / jobs.Count, detail)));
                        var ai = await Task.Run(() => upscaler.Upscale(job.Source, job.Factor, tileProgress, cancel), cancel);
                        results[job] = strength >= 0.999 ? ai
                            : Mix(Core.Transforms.Resampler.Resize(job.Source, ai.Width, ai.Height, Core.Transforms.ResampleMode.Lanczos), ai, strength);
                    }
                });
        }
        catch (Exception ex)
        {
            _main.Dialogs.ShowError($"AI Upscale failed:\n\n{ex.Message}");
            return;
        }
        if (!finished)
        {
            _main.SetStatus("AI upscale cancelled. Nothing was changed.");
            return;
        }

        if (scale == 1)
        {
            var job = jobs[0];
            if (job.Image is { } image)
            {
                // A placed picture stays a movable, resizable picture: only its pixels get better.
                _main.Push(ObjectChangeAction.Apply("AI Clean Up", job.Layer, image with { Source = results[job] }));
            }
            else
            {
                var layer = job.Layer;
                var before = layer.Pixels.Clone();
                layer.Pixels.CopyFrom(results[job], 0, 0);
                layer.NotifyPixelsChanged();
                _main.Push(PixelChangeAction.Create("AI Clean Up", layer, before, layer.Pixels.Bounds));
            }
        }
        else
        {
            var byPixels = jobs.Where(j => j.Image == null).ToDictionary(j => j.Layer.Pixels, j => results[j]);
            // Matched by the exact object (not by value), so only the pictures that were processed change.
            var byImage = new Dictionary<LayerObject, PixelBuffer>(ReferenceEqualityComparer.Instance);
            foreach (var job in jobs.Where(j => j.Image != null)) byImage[job.Image!] = results[job];
            _main.Push(CanvasChangeAction.Apply($"AI Upscale {scale:0}x", Doc,
                p => byPixels.TryGetValue(p, out var r) ? r : Core.Transforms.Resampler.Resize(p, w, h, Core.Transforms.ResampleMode.Lanczos),
                (o, _, _) =>
                {
                    var scaled = ObjectTransforms.Scale(o, scale, scale);
                    return byImage.TryGetValue(o, out var sharper) && scaled is ImageObject img ? img with { Source = sharper } : scaled;
                }));
            _main.Canvas?.FitToWindow();
        }
        _main.SetStatus(scale == 1
            ? $"AI clean-up done (on {device}). Ctrl+Z to undo; Ctrl+Y to compare again."
            : $"AI upscale done on {device}: now {Doc.Width} x {Doc.Height} px. Zoom in (Ctrl+1 = 100%) to see the detail. Ctrl+Z to undo.");
    }

    /// <summary>
    /// What to improve on a layer: its pixels, or (for a placed picture) its own source pixels, enlarged
    /// enough to stay sharp at the size it will be shown. Shapes, text and empty layers: nothing.
    /// </summary>
    private Job? MakeJob(Layer layer)
    {
        double scale = _pendingScale;
        switch (layer.Object)
        {
            case ImageObject image:
            {
                // Displayed size after the change, compared with the picture's own pixels.
                double factor = Math.Clamp(image.Box.Width * scale / image.Source.Width, 1, 4);
                factor = Math.Round(factor * 4) / 4; // tidy steps
                return new Job(layer, image.Source, factor, image);
            }
            case null when !layer.Pixels.VisibleBounds().IsEmpty:
                return new Job(layer, layer.Pixels.Clone(), scale, null); // a private copy: safe on another thread
            default:
                return null;
        }
    }

    /// <summary>Blends the plain resize with the AI result (strength 0..1).</summary>
    private static PixelBuffer Mix(PixelBuffer plain, PixelBuffer ai, double strength)
    {
        var result = ai.Clone();
        for (int i = 0; i < result.Data.Length; i++)
            if ((i & 3) != 3) result.Data[i] = ColorMath.ClampByte(plain.Data[i] + (ai.Data[i] - plain.Data[i]) * strength);
        return result;
    }
}
