using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>Draws an object into pixels. Implemented by the app (it needs a vector/text engine).</summary>
public interface IObjectRenderer
{
    /// <summary>Returns a canvas-sized buffer with only the object drawn on it.</summary>
    PixelBuffer Render(LayerObject obj, int canvasWidth, int canvasHeight);

    /// <summary>Natural size of a text object's text (used to size its box).</summary>
    (double Width, double Height) MeasureText(TextObject text);
}

/// <summary>
/// Holds the renderer the app registered at startup. The engine stays free of UI code, while layers
/// and undo steps can still redraw objects.
/// </summary>
public static class ObjectRendering
{
    private static IObjectRenderer? _renderer;

    public static IObjectRenderer Renderer
    {
        get => _renderer ?? throw new InvalidOperationException("No object renderer registered (ObjectRendering.Renderer).");
        set => _renderer = value;
    }

    public static bool IsAvailable => _renderer != null;

    /// <summary>Gives a text object a box that fits its text (keeping the box center and rotation).</summary>
    public static TextObject FitTextBox(TextObject text)
    {
        var (w, h) = Renderer.MeasureText(text);
        return text with { Box = ObjectBox.FromCenter(text.Box.Center, Math.Max(1, w), Math.Max(1, h), text.Box.Rotation) };
    }
}
