using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>Paint over a blemish (a spot, a speck, a wire): it is healed from a clean place nearby that the program picks.</summary>
public sealed class SpotHealingTool : RetouchToolBase
{
    public SpotHealingTool(IToolHost host) : base(host, new RetouchOptions(RetouchParts.Hardness) { Size = 24 }) { }

    public override string Name => "Spot Healing";
    public override string IconKey => "SpotHeal";
    public override Key Shortcut => Key.J;

    protected override double StrokeOpacity => 1.0;

    // While the mouse is down the painted area is only shown (tinted); the healing happens when it is released.
    protected override IRetouchOp? BeginOperation(ToolMouseArgs e, Layer layer) => new HighlightOp(new ColorRgba(255, 60, 60), 0.45);

    protected override bool EndStroke(BrushStroke stroke, Layer layer)
    {
        var offset = SpotHeal.FindOffset(stroke.Original, stroke.TotalDirty);
        if (offset is not { } found)
        {
            Host.SetStatus("No clean area nearby to heal from. Try a smaller brush or a spot nearer to the middle of the picture.");
            return false;
        }
        layer.NotifyPixelsChanged(stroke.Reapply(new HealOp(found.Dx, found.Dy, Math.Clamp((int)(Settings.Size / 2) + 6, 6, 120))));
        return true;
    }
}
