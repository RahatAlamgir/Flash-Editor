using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// A placed picture that can be moved, resized and rotated without losing quality: it is always
/// redrawn from the original <see cref="Source"/> pixels.
/// </summary>
/// <param name="Original">
/// Optional: the picture as it was before parts of it were removed (same size as <see cref="Source"/>), so the Refine
/// brush can paint removed parts back. Set when the background of a picture is removed.
/// </param>
/// <param name="Revision">Goes up each time the pixels of <see cref="Source"/> are edited in place, so the edit counts as a change.</param>
public sealed record ImageObject(
    ObjectBox Box,
    PixelBuffer Source,
    string Name = "Image",
    bool FlipX = false,
    bool FlipY = false,
    PixelBuffer? Original = null,
    int Revision = 0) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Image";
    public override string SuggestedLayerName => Name;

    /// <summary>Places <paramref name="source"/> centered on the canvas, scaled down to fit when it is larger.</summary>
    public static ImageObject CenteredOn(PixelBuffer source, int canvasWidth, int canvasHeight, string name, double maxFill = 1.0)
    {
        double scale = Math.Min(1.0, Math.Min(canvasWidth * maxFill / source.Width, canvasHeight * maxFill / source.Height));
        double w = Math.Max(1, source.Width * scale), h = Math.Max(1, source.Height * scale);
        return new ImageObject(new ObjectBox((canvasWidth - w) / 2, (canvasHeight - h) / 2, w, h), source, name);
    }

    /// <summary>
    /// Where a point of the canvas lies in the picture's own pixels (0,0 = top-left pixel), however the picture is
    /// moved, resized, rotated or mirrored. Used to paint on the picture itself instead of on its screen image.
    /// </summary>
    public PointD ToSource(PointD canvasPoint)
    {
        var local = Box.ToLocal(canvasPoint);                 // relative to the center, turned back upright
        double x = FlipX ? -local.X : local.X;
        double y = FlipY ? -local.Y : local.Y;
        return new PointD((x + Box.Width / 2) * Source.Width / Box.Width,
                          (y + Box.Height / 2) * Source.Height / Box.Height);
    }

    /// <summary>
    /// True when the picture has visible content at (or within <paramref name="radius"/> canvas pixels of) a point of the canvas. Clicks use
    /// this, so the removed (transparent) part of a cut-out can be clicked through to what lies behind it.
    /// </summary>
    public bool HasContentAt(PointD canvasPoint, double radius = 0, byte minAlpha = 16)
    {
        if (!Box.Contains(canvasPoint, radius)) return false;
        double reach = radius * SourcePixelsPerCanvasPixel;
        double step = Math.Max(1, reach);
        for (double dy = -reach; dy <= reach + 1e-9; dy += step)
            for (double dx = -reach; dx <= reach + 1e-9; dx += step)
            {
                var s = ToSource(canvasPoint);
                int u = (int)Math.Floor(s.X + dx), v = (int)Math.Floor(s.Y + dy);
                if (u >= 0 && v >= 0 && u < Source.Width && v < Source.Height && Source.GetPixel(u, v).A >= minAlpha) return true;
            }
        return false;
    }

    /// <summary>How many of the picture's own pixels one canvas pixel covers (2 = the picture is shown at half size).</summary>
    public double SourcePixelsPerCanvasPixel
        => Math.Sqrt(Source.Width / Math.Max(1e-9, Box.Width) * (Source.Height / Math.Max(1e-9, Box.Height)));

    /// <summary>The same picture with edited pixels. The edited buffer must be a different buffer than the old one, so undo can swap them.</summary>
    public ImageObject WithEditedSource(PixelBuffer edited, PixelBuffer original)
        => this with { Source = edited, Original = original, Revision = Revision + 1 };
}
