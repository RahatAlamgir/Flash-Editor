using System.Windows;
using System.Windows.Input;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Tools;

/// <summary>Mouse information passed to tools.</summary>
/// <param name="Position">Position in image pixels (fractional).</param>
/// <param name="ViewportPosition">Position in the canvas viewport (screen units), used for panning/zooming.</param>
/// <param name="Button">Button that was pressed/released (null for moves).</param>
public sealed record ToolMouseArgs(
    PointD Position,
    Point ViewportPosition,
    MouseButton? Button,
    bool IsLeftDown,
    ModifierKeys Modifiers,
    int ClickCount)
{
    public int X => (int)Math.Floor(Position.X);
    public int Y => (int)Math.Floor(Position.Y);
    public bool Shift => Modifiers.HasFlag(ModifierKeys.Shift);
    public bool Alt => Modifiers.HasFlag(ModifierKeys.Alt);
    public bool Ctrl => Modifiers.HasFlag(ModifierKeys.Control);
}
