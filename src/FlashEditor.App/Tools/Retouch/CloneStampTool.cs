using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>Paints a copy of another part of the picture (remove an object, repeat a pattern).</summary>
public sealed class CloneStampTool : SourceToolBase
{
    public CloneStampTool(IToolHost host)
        : base(host, new RetouchOptions(RetouchParts.Strength | RetouchParts.Aligned | RetouchParts.Hardness)) { }

    public override string Name => "Clone Stamp";
    public override string IconKey => "Clone";
    public override Key Shortcut => Key.S;

    protected override IRetouchOp CreateOperation(int dx, int dy) => new CloneOp(dx, dy);
}
