using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// A picture shown inside a shape (a frame). The picture is part of the shape, so moving, resizing, turning or copying the shape takes the
/// picture along; the picture covers the whole shape and is clipped to its outline.
/// </summary>
/// <param name="Zoom">1 = the picture just covers the shape; larger zooms in (at most 5).</param>
/// <param name="PanX">-1 .. 1: which part shows. 0 = centered, -1 = the left edge of the picture, 1 = the right edge.</param>
/// <param name="PanY">-1 .. 1: the same, up and down.</param>
public sealed record PictureFill(PixelBuffer Image, string Name = "Picture", double Zoom = 1, double PanX = 0, double PanY = 0)
{
    /// <summary>Where the picture lies in the shape's own space (0,0 = top-left of the shape), larger than the shape when zoomed or not the same shape.</summary>
    public (double X, double Y, double Width, double Height) Place(double shapeWidth, double shapeHeight)
    {
        double scale = Math.Max(shapeWidth / Math.Max(1, Image.Width), shapeHeight / Math.Max(1, Image.Height)) * Math.Clamp(Zoom, 1, 5);
        double w = Image.Width * scale, h = Image.Height * scale;
        double x = (shapeWidth - w) / 2 + Math.Clamp(PanX, -1, 1) * (w - shapeWidth) / 2;
        double y = (shapeHeight - h) / 2 + Math.Clamp(PanY, -1, 1) * (h - shapeHeight) / 2;
        return (x, y, w, h);
    }
}
