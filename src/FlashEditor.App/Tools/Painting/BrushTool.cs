using System.Windows.Input;

namespace FlashEditor.App.Tools.Painting;

/// <summary>Paints with the foreground color.</summary>
public sealed class BrushTool : PaintToolBase
{
    public BrushTool(IToolHost host) : base(host) { }

    public override string Name => "Brush";
    public override string IconKey => "Brush";
    public override Key Shortcut => Key.B;
    protected override bool IsEraser => false;
}
