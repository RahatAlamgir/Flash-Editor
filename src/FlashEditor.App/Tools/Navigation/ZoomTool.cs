using System.Windows.Input;

namespace FlashEditor.App.Tools.Navigation;

/// <summary>Click to zoom in; Alt+click or right-click to zoom out.</summary>
public sealed class ZoomTool : ToolBase
{
    private const double Step = 1.5;

    public ZoomTool(IToolHost host) : base(host) { }

    public override string Name => "Zoom";
    public override string IconKey => "Zoom";
    public override Key Shortcut => Key.Z;
    public override object Options => "Click to zoom in. Alt+click or right-click to zoom out. Ctrl+0 fits the image to the window.";

    public override void MouseDown(ToolMouseArgs e)
    {
        if (Host.Canvas is not { } canvas) return;
        bool zoomOut = e.Button == MouseButton.Right || e.Alt;
        canvas.ZoomAt(e.ViewportPosition, zoomOut ? canvas.Zoom / Step : canvas.Zoom * Step);
    }
}
