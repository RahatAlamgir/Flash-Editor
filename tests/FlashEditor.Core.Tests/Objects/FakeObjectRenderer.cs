using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>
/// Stand-in for the app's WPF renderer: paints an object's (unrotated) box in solid red.
/// Enough to test that layers, undo and canvas changes redraw objects in the right place.
/// </summary>
internal sealed class FakeObjectRenderer : IObjectRenderer
{
    public static readonly ColorRgba Paint = new(255, 0, 0);

    public static void Install() => ObjectRendering.Renderer = new FakeObjectRenderer();

    public PixelBuffer Render(LayerObject obj, int canvasWidth, int canvasHeight)
    {
        var canvas = new PixelBuffer(canvasWidth, canvasHeight);
        var area = obj.Box.Bounds().Intersect(canvas.Bounds);
        for (int y = area.Y; y < area.Bottom; y++)
            for (int x = area.X; x < area.Right; x++)
                canvas.SetPixel(x, y, Paint);
        return canvas;
    }

    public (double Width, double Height) MeasureText(TextObject text) => (text.Text.Length * 10, text.FontSize);
}
