using System.Windows.Input;

namespace FlashEditor.App.Tools.Painting;

/// <summary>Makes pixels of the active layer transparent.</summary>
public sealed class EraserTool : PaintToolBase
{
    public EraserTool(IToolHost host) : base(host) { }

    public override string Name => "Eraser";
    public override string IconKey => "Eraser";
    public override Key Shortcut => Key.E;
    protected override bool IsEraser => true;
}
