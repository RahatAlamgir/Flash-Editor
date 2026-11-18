using System.Windows.Input;
using FlashEditor.AI.Models;
using FlashEditor.AI.Segmentation;
using FlashEditor.App.Tools.Selecting;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>Click to select (AI): finds the object under a click or inside a box and selects it. Needs the two MobileSAM model files.</summary>
public sealed class ClickSelectCommands
{
    private readonly MainViewModel _main;
    private ClickSegmenter? _segmenter;

    public ClickSelectCommands(MainViewModel main) => _main = main;

    /// <summary>Lets go of the loaded models (before their files are deleted).</summary>
    public void Release()
    {
        _segmenter?.Dispose();
        _segmenter = null;
    }

    public void Run(ClickPrompt prompt, ModifierKeys modifiers)
    {
        const string title = "Click Select (AI)";
        var models = _main.Effects.Models;
        _main.Effects.EnsureModel(ModelInfo.MobileSamEncoder, title, () =>
            _main.Effects.EnsureModel(ModelInfo.MobileSamDecoder, title, () => Predict(prompt, modifiers, models)));
    }

    private void Predict(ClickPrompt prompt, ModifierKeys modifiers, ModelStore models)
    {
        var doc = _main.Document.Model;
        var segmenter = _segmenter ??= new ClickSegmenter(models.PathOf(ModelInfo.MobileSamEncoder), models.PathOf(ModelInfo.MobileSamDecoder));
        var flat = Compositor.Flatten(doc);              // the AI sees what you see
        _main.SetStatus(segmenter.DeviceName.Length == 0
            ? "The AI is looking at the picture (the first time takes a few seconds)..."
            : "The AI is selecting...");
        _main.RunAsync("Click Select", async () =>
        {
            var mask = await Task.Run(() => segmenter.Predict(flat, prompt));
            if (!ReferenceEquals(doc, _main.Document.Model)) return;
            var drawn = Selection.FromMask(mask, doc.Width, doc.Height);
            _main.SetStatus(drawn == null
                ? "The AI found nothing there. Click on the object itself, or drag a box around it."
                : SelectionCombiner.Apply(doc, drawn, modifiers) + " Click again with Shift to add more, Alt to take away.");
        });
    }
}
