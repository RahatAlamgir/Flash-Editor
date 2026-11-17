using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>Like Clone Stamp, but the copied detail takes on the light and color of the place it is painted on.</summary>
public sealed class HealingBrushTool : SourceToolBase
{
    public HealingBrushTool(IToolHost host)
        : base(host, new RetouchOptions(RetouchParts.Strength | RetouchParts.Aligned | RetouchParts.Hardness)) { }

    public override string Name => "Healing Brush";
    public override string IconKey => "Heal";
    public override Key Shortcut => Key.J;

    protected override IRetouchOp CreateOperation(int dx, int dy)
        => new HealOp(dx, dy, Math.Clamp((int)(Settings.Size / 2) + 6, 6, 120));
}
