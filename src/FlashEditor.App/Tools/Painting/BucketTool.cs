using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.History;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Painting;

/// <summary>Fills an area of similar color with the foreground color.</summary>
public sealed class BucketTool : ToolBase
{
    private readonly BucketOptions _options = new();

    public BucketTool(IToolHost host) : base(host) { }

    public override string Name => "Paint Bucket";
    public override string IconKey => "Bucket";
    public override Key Shortcut => Key.G;
    public override object Options => _options;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left || !TryGetEditableLayer(out var layer)) return;
        if (!Document.Bounds.Contains(e.X, e.Y)) return;

        var before = layer.Pixels.Clone();
        var dirty = FloodFill.Fill(layer.Pixels, e.X, e.Y, Host.Colors.Foreground,
            (int)_options.Tolerance, _options.Contiguous, Document.Selection);
        layer.NotifyPixelsChanged(dirty);
        Push(PixelChangeAction.Create(Name, layer, before, dirty));
    }
}
