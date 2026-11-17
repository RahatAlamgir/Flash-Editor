using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>Soften (Blur) or crisp up (Sharpen) the part of the picture you paint over. Alt = the other one while painting.</summary>
public sealed class BlurSharpenTool : RetouchToolBase
{
    public BlurSharpenTool(IToolHost host)
        : base(host, new RetouchOptions(RetouchParts.Strength | RetouchParts.BlurSharpen | RetouchParts.Hardness, strength: 50)) { }

    public override string Name => "Blur / Sharpen";
    public override string IconKey => "BlurSharpen";
    public override Key Shortcut => Key.Q;

    // Blur: a stronger setting blurs wider; Sharpen: it sharpens harder.
    protected override double StrokeOpacity => Settings.Sharpen ? 1.0 : 0.35 + 0.65 * Settings.Strength / 100.0;

    protected override IRetouchOp? BeginOperation(ToolMouseArgs e, Layer layer)
    {
        double s = Settings.Strength / 100.0;
        return Settings.Sharpen != e.Alt
            ? new SharpenOp(2, 0.3 + 1.7 * s)
            : new BlurOp((int)Math.Round(1 + 7 * s));
    }
}
