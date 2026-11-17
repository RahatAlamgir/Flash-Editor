using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>Paint light onto (Dodge) or shadow into (Burn) parts of a picture. Alt = the other one while painting.</summary>
public sealed class DodgeBurnTool : RetouchToolBase
{
    public DodgeBurnTool(IToolHost host)
        : base(host, new RetouchOptions(RetouchParts.Strength | RetouchParts.DodgeBurn | RetouchParts.Hardness, strength: 30)) { }

    public override string Name => "Dodge / Burn";
    public override string IconKey => "Tone";
    public override Key Shortcut => Key.O;

    protected override IRetouchOp? BeginOperation(ToolMouseArgs e, Layer layer)
        => new ToneOp(burn: Settings.Burn != e.Alt, Settings.Range);
}
