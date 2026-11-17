using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// Moves objects along when the whole image changes (canvas size, resize, rotate, flip), so object
/// layers stay editable instead of being turned into pixels.
/// </summary>
public static class ObjectTransforms
{
    public static LayerObject Offset(LayerObject obj, double dx, double dy) => WithBox(obj, obj.Box.Offset(dx, dy));

    /// <summary>Scales position and size (image resize). Text font size scales too.</summary>
    public static LayerObject Scale(LayerObject obj, double sx, double sy)
    {
        var b = obj.Box;
        var box = new ObjectBox(b.X * sx, b.Y * sy, b.Width * sx, b.Height * sy, b.Rotation);
        var scaled = obj switch
        {
            TextObject t => t with { Box = box, FontSize = t.FontSize * Math.Sqrt(sx * sy) },
            ShapeObject s => s with { Box = box, StrokeWidth = s.StrokeWidth * Math.Sqrt(sx * sy), CornerRadius = s.CornerRadius * Math.Sqrt(sx * sy) },
            _ => WithBox(obj, box),
        };
        return scaled.Shadow is { } shadow ? scaled with { Shadow = shadow.Scaled(Math.Sqrt(sx * sy)) } : scaled;
    }

    /// <summary>Mirrors left-right across a canvas of width <paramref name="canvasWidth"/>.</summary>
    public static LayerObject FlipHorizontal(LayerObject obj, int canvasWidth)
    {
        var c = obj.Box.Center;
        var box = obj.Box.WithCenter(new PointD(canvasWidth - c.X, c.Y)) with { Rotation = -obj.Box.Rotation };
        var flipped = WithBox(obj, box) with { FlipX = !obj.FlipX };
        return obj.Shadow is { } s ? flipped with { Shadow = s with { Angle = ObjectBox.NormalizeAngle(180 - s.Angle) } } : flipped;
    }

    /// <summary>Mirrors top-bottom across a canvas of height <paramref name="canvasHeight"/>.</summary>
    public static LayerObject FlipVertical(LayerObject obj, int canvasHeight)
    {
        var c = obj.Box.Center;
        var box = obj.Box.WithCenter(new PointD(c.X, canvasHeight - c.Y)) with { Rotation = -obj.Box.Rotation };
        var flipped = WithBox(obj, box) with { FlipY = !obj.FlipY };
        return obj.Shadow is { } s ? flipped with { Shadow = s with { Angle = ObjectBox.NormalizeAngle(-s.Angle) } } : flipped;
    }

    /// <summary>
    /// Rotates around the old canvas center by <paramref name="degrees"/> (clockwise), when the canvas
    /// changes from old size to new size (the new canvas is centered on the old one).
    /// </summary>
    public static LayerObject Rotate(LayerObject obj, double degrees, int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        double a = degrees * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        var c = obj.Box.Center;
        double dx = c.X - oldWidth / 2.0, dy = c.Y - oldHeight / 2.0;
        var center = new PointD(newWidth / 2.0 + dx * cos - dy * sin, newHeight / 2.0 + dx * sin + dy * cos);
        var box = obj.Box.WithCenter(center) with { Rotation = ObjectBox.NormalizeAngle(obj.Box.Rotation + degrees) };
        var rotated = WithBox(obj, box);
        return obj.Shadow is { } s ? rotated with { Shadow = s.Rotated(degrees) } : rotated;
    }

    public static LayerObject WithBox(LayerObject obj, ObjectBox box) => obj with { Box = box };
}
