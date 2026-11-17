using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>Draws an object into pixels. Implemented by the app (it needs a vector/text engine).</summary>
public interface IObjectRenderer
{
    /// <summary>Returns a canvas-sized buffer with only the object drawn on it.</summary>
    PixelBuffer Render(LayerObject obj, int canvasWidth, int canvasHeight);

    /// <summary>Natural size of a text object's text (used to size its box).</summary>
    (double Width, double Height) MeasureText(TextObject text);

    /// <summary>
    /// The part of a canvas of the given size that <see cref="RenderInto"/> draws on (the object plus its border and shadow).
    /// Lets dragging an object redraw only that part instead of the whole canvas. The default is the whole canvas.
    /// </summary>
    PixelRect PaintedArea(LayerObject obj, int canvasWidth, int canvasHeight) => new(0, 0, canvasWidth, canvasHeight);

    /// <summary>
    /// The whole rectangle the object's drawing covers (box, border, shadow), even where it is outside the canvas.
    /// Empty when unknown (the default); then <see cref="Layer"/> always redraws instead of just shifting the old drawing.
    /// </summary>
    PixelRect DrawingBounds(LayerObject obj) => PixelRect.Empty;

    /// <summary>
    /// Draws the object into an existing canvas-sized buffer, replacing everything inside <see cref="PaintedArea"/>
    /// (and nothing outside it). The default draws a whole new canvas and copies it over.
    /// </summary>
    void RenderInto(PixelBuffer canvas, LayerObject obj) => canvas.CopyFrom(Render(obj, canvas.Width, canvas.Height), 0, 0);
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
