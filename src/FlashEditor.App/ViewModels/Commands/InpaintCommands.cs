using FlashEditor.AI.Inpainting;
using FlashEditor.AI.Models;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>
/// Remove / Fill Selection (AI): select an area (an object, a person, a logo, a blemish) and the AI paints in what would plausibly be there.
/// The result goes on a new layer called "AI Fill" above the active layer, so the original is never changed and the fill can be hidden,
/// moved, erased or undone.
/// </summary>
public sealed class InpaintCommands
{
    private readonly MainViewModel _main;
    private Inpainter? _inpainter;

    public InpaintCommands(MainViewModel main)
    {
        _main = main;
        FillSelectionCommand = new RelayCommand(FillSelection);
    }

    public RelayCommand FillSelectionCommand { get; }

    /// <summary>Lets go of the loaded model (before its file is deleted).</summary>
    public void Release()
    {
        _inpainter?.Dispose();
        _inpainter = null;
    }

    private Document Doc => _main.Document.Model;

    private void FillSelection()
    {
        var selection = Doc.Selection;
        if (selection == null || selection.IsEmpty)
        {
            _main.Dialogs.ShowInfo("Remove / Fill Selection (AI)",
                "First select the area you want to remove or fill:\n\n" +
                "  • Lasso (L) or Rectangle (M): draw around it\n" +
                "  • Magic Wand (W): click on it\n" +
                "  • Select > Select Subject (AI): for a whole person or object\n\n" +
                "Select a little more than the object itself (include its shadow). Then run this again: the AI paints in what would be behind it.");
            return;
        }

        var model = ModelInfo.LamaInpainting;
        _main.Effects.EnsureModel(model, "Remove / Fill Selection (AI)", () =>
        {
            _inpainter ??= new Inpainter(model, _main.Effects.Models.PathOf(model));
            Run(selection.Mask.ToArray(), selection.Bounds);
        });
    }

    private void Run(byte[] mask, Core.Imaging.PixelRect bounds)
    {
        var doc = Doc;
        var inpainter = _inpainter!;
        var flat = Compositor.Flatten(doc);               // the AI sees what you see, all visible layers together
        bool big = bounds.Width * (long)bounds.Height > flat.Width * (long)flat.Height / 3;
        _main.SetStatus(inpainter.DeviceName.Length == 0
            ? "The AI is filling the selection (the first time takes a little longer while the model loads)..."
            : "The AI is filling the selection...");

        _main.RunAsync("Remove / Fill (AI)", async () =>
        {
            var progress = new Progress<double>(p => _main.SetStatus(p >= 1 ? "The AI is finishing the fill..." : $"The AI is filling the selection... {p:P0}"));
            var fill = await Task.Run(() => inpainter.Fill(flat, mask, progress));
            if (fill == null || !ReferenceEquals(doc, Doc)) return;           // nothing selected, or another picture was shown meanwhile

            var layer = new Layer("AI Fill", fill);
            int top = Math.Max(0, doc.ActiveLayerIndex);
            _main.Push(LayerStructureAction.Record("Remove / Fill (AI)", doc, () =>
            {
                var list = doc.Layers.ToList();
                list.Insert(Math.Min(top + 1, list.Count), layer);
                doc.SetLayers(list, layer);
            }));
            _main.SelectOnly(layer);
            _main.SetStatus(big
                ? "Filled. A large area is filled piece by piece; if some part looks off, undo and fill that part on its own. Ctrl+Z undoes it."
                : "Filled on a new layer \"AI Fill\". Hide it to compare, or press Ctrl+Z to undo.");
        });
    }
}
