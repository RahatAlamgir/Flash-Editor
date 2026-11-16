using System.Windows;
using System.Windows.Input;

namespace FlashEditor.App.Tools.Navigation;

/// <summary>Drag to scroll around the image. (Holding Space or the middle mouse button does the same with any tool.)</summary>
public sealed class HandTool : ToolBase
{
    private Point? _last;

    public HandTool(IToolHost host) : base(host) { }

    public override string Name => "Hand";
    public override string IconKey => "Hand";
    public override Key Shortcut => Key.H;
    public override Cursor Cursor => Cursors.Hand;
    public override object Options => "Drag to pan. Tip: hold Space or the middle mouse button to pan with any tool; scroll the wheel to zoom.";

    public override void MouseDown(ToolMouseArgs e) => _last = e.ViewportPosition;

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_last is not Point last) return;
        Host.Canvas?.PanBy(e.ViewportPosition - last);
        _last = e.ViewportPosition;
    }

    public override void MouseUp(ToolMouseArgs e) => _last = null;
}
