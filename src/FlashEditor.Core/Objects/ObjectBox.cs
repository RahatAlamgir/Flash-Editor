using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// Position and size of an editable object on the canvas: an unrotated box (top-left X/Y, Width,
/// Height in image pixels) that is then rotated by <see cref="Rotation"/> degrees (clockwise) around
/// its center.
/// </summary>
public readonly record struct ObjectBox(double X, double Y, double Width, double Height, double Rotation = 0)
{
    public PointD Center => new(X + Width / 2, Y + Height / 2);

    public static ObjectBox FromCenter(PointD center, double width, double height, double rotation = 0)
        => new(center.X - width / 2, center.Y - height / 2, width, height, rotation);

    public ObjectBox Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

    public ObjectBox WithCenter(PointD center) => FromCenter(center, Width, Height, Rotation);

    /// <summary>Canvas position of a point given in box coordinates relative to the center (before rotation).</summary>
    public PointD ToCanvas(double localX, double localY)
    {
        double a = Rotation * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        var c = Center;
        return new PointD(c.X + localX * cos - localY * sin, c.Y + localX * sin + localY * cos);
    }

    /// <summary>Box coordinates (relative to the center, unrotated) of a canvas point.</summary>
    public PointD ToLocal(PointD canvasPoint)
    {
        double a = -Rotation * Math.PI / 180, cos = Math.Cos(a), sin = Math.Sin(a);
        var c = Center;
        double dx = canvasPoint.X - c.X, dy = canvasPoint.Y - c.Y;
        return new PointD(dx * cos - dy * sin, dx * sin + dy * cos);
    }

    /// <summary>True when the canvas point lies inside the (rotated) box, grown by <paramref name="margin"/>.</summary>
    public bool Contains(PointD canvasPoint, double margin = 0)
    {
        var p = ToLocal(canvasPoint);
        return Math.Abs(p.X) <= Width / 2 + margin && Math.Abs(p.Y) <= Height / 2 + margin;
    }

    /// <summary>
    /// True when the (rotated) box and the upright rectangle <paramref name="left"/>, <paramref name="top"/>,
    /// <paramref name="right"/>, <paramref name="bottom"/> overlap or touch (used by drag-selecting with a rubber band).
    /// Exact for rotated boxes: a box whose corner area is empty near the rectangle is not counted.
    /// </summary>
    public bool IntersectsRect(double left, double top, double right, double bottom)
    {
        // Two convex shapes overlap unless some edge direction of either one separates them.
        // 1) Upright axes: compare the box's bounding extent with the rectangle.
        var corners = Corners();
        if (corners.Max(p => p.X) < left || corners.Min(p => p.X) > right) return false;
        if (corners.Max(p => p.Y) < top || corners.Min(p => p.Y) > bottom) return false;

        // 2) The box's own axes: look at the rectangle's corners from inside the box.
        var local = new[] { ToLocal(new PointD(left, top)), ToLocal(new PointD(right, top)), ToLocal(new PointD(right, bottom)), ToLocal(new PointD(left, bottom)) };
        double hw = Width / 2, hh = Height / 2;
        if (local.Max(p => p.X) < -hw || local.Min(p => p.X) > hw) return false;
        if (local.Max(p => p.Y) < -hh || local.Min(p => p.Y) > hh) return false;
        return true;
    }

    /// <summary>The four corners on the canvas: top-left, top-right, bottom-right, bottom-left.</summary>
    public PointD[] Corners() => new[]
    {
        ToCanvas(-Width / 2, -Height / 2), ToCanvas(Width / 2, -Height / 2),
        ToCanvas(Width / 2, Height / 2), ToCanvas(-Width / 2, Height / 2),
    };

    /// <summary>Axis-aligned rectangle around the rotated box, grown by <paramref name="margin"/>.</summary>
    public PixelRect Bounds(double margin = 0)
    {
        var c = Corners();
        double l = c.Min(p => p.X) - margin, t = c.Min(p => p.Y) - margin;
        double r = c.Max(p => p.X) + margin, b = c.Max(p => p.Y) + margin;
        return PixelRect.FromBounds(l, t, r, b);
    }

    /// <summary>Rotation normalized to -180..180.</summary>
    public static double NormalizeAngle(double degrees)
    {
        degrees %= 360;
        if (degrees > 180) degrees -= 360;
        if (degrees <= -180) degrees += 360;
        return degrees;
    }
}
