using System.Windows.Input;
using FlashEditor.AI.Segmentation;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Click to select (AI): click on an object and the AI selects just that object; or drag a box around it for a more exact pick.
/// Shift adds to the selection, Alt takes away from it (like the Magic Wand).
/// </summary>
public sealed class ClickSelectTool : ToolBase
{
    private PointD _start;
    private System.Windows.Point _startView;
    private bool _down;

    public ClickSelectTool(IToolHost host) : base(host) { }

    public override string Name => "Click Select (AI)";
    public override string IconKey => "ClickSelect";
    public override Key Shortcut => Key.W;
    public override object Options => "Click an object to select it, or drag a box around it. Shift = add, Alt = subtract.";

    public override void Activate()
        => Host.SetStatus("Click Select (AI): click on an object to select it, or drag a box around it. Shift adds, Alt subtracts.");

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        _start = e.Position;
        _startView = e.ViewportPosition;
        _down = true;
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (!_down) return;
        _down = false;
        double moved = Math.Abs(e.ViewportPosition.X - _startView.X) + Math.Abs(e.ViewportPosition.Y - _startView.Y);
        var bounds = Document.Bounds;
        double Cx(double v) => Math.Clamp(v, 0, bounds.Width - 1);
        double Cy(double v) => Math.Clamp(v, 0, bounds.Height - 1);
        ClickPrompt prompt = moved < 6
            ? new ClickPrompt(new[] { (Cx(_start.X), Cy(_start.Y), true) })
            : new ClickPrompt(Array.Empty<(double, double, bool)>(), (Cx(_start.X), Cy(_start.Y), Cx(e.Position.X), Cy(e.Position.Y)));
        Host.RunClickSelect(prompt, e.Modifiers);
    }
}
