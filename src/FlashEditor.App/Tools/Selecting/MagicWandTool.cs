using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Magic wand: click a color to select the area of similar color.
/// Shift = add, Alt = subtract. Tolerance and "connected only" are in the options bar.
/// </summary>
public sealed class MagicWandTool : ToolBase
{
    private readonly MagicWandOptions _options = new();

    public MagicWandTool(IToolHost host) : base(host) { }

    public override string Name => "Magic Wand";
    public override string IconKey => "MagicWand";
    public override Key Shortcut => Key.W;
    public override object Options => _options;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        if (e.X < 0 || e.Y < 0 || e.X >= Document.Width || e.Y >= Document.Height) return;

        // Sample what you see (all layers) or only the selected layer.
        var source = _options.SampleAllLayers || Document.ActiveLayer == null
            ? Compositor.Flatten(Document)
            : Document.ActiveLayer.Pixels;
        var mask = FloodFill.BuildMask(source, e.X, e.Y, (int)_options.Tolerance, _options.Contiguous);
        // The fill mask marks matching pixels with 1; a selection needs 255 for "fully selected".
        for (int i = 0; i < mask.Length; i++) if (mask[i] != 0) mask[i] = 255;
        var drawn = Selection.FromMask(mask, Document.Width, Document.Height);
        Host.SetStatus(SelectionCombiner.Apply(Document, drawn, e.Modifiers));
    }
}
