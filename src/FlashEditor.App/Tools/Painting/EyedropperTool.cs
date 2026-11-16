using System.Windows.Input;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Painting;

/// <summary>Picks a color from the image (all visible layers merged).</summary>
public sealed class EyedropperTool : ToolBase
{
    private bool _pickBackground;
    private bool _dragging;

    public EyedropperTool(IToolHost host) : base(host) { }

    public override string Name => "Eyedropper";
    public override string IconKey => "Eyedropper";
    public override Key Shortcut => Key.I;
    public override object Options => "Click to pick the foreground color. Alt+click or right-click picks the background color.";

    public override void MouseDown(ToolMouseArgs e)
    {
        _pickBackground = e.Button == MouseButton.Right || e.Alt;
        _dragging = true;
        Pick(e);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_dragging) Pick(e);
    }

    public override void MouseUp(ToolMouseArgs e) => _dragging = false;

    private void Pick(ToolMouseArgs e)
    {
        if (!Document.Bounds.Contains(e.X, e.Y)) return;
        var color = Compositor.SampleMerged(Document, e.X, e.Y);
        if (color.A == 0) return; // nothing visible there
        color = color with { A = 255 };

        if (_pickBackground) Host.Colors.Background = color;
        else Host.Colors.Foreground = color;
        Host.SetStatus($"Picked {color.ToHex()}");
    }
}
