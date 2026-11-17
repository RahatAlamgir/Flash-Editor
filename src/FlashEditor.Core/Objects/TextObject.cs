using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

public enum TextAlign { Left, Center, Right }

/// <summary>
/// Editable text. The box size follows the text (measured by the renderer); dragging a corner
/// scales the font size.
/// </summary>
public sealed record TextObject(
    ObjectBox Box,
    string Text,
    string FontFamily,
    double FontSize,
    ColorRgba Color,
    bool Bold = false,
    bool Italic = false,
    TextAlign Align = TextAlign.Left,
    bool FlipX = false,
    bool FlipY = false,
    double OutlineWidth = 0,
    ColorRgba? OutlineColor = null,
    double LineSpacing = 1.0) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Text";

    public override string SuggestedLayerName
    {
        get
        {
            string first = Text.Split('\n')[0].Trim();
            return first.Length == 0 ? "Text" : first.Length > 24 ? first[..24] + "..." : first;
        }
    }
}
