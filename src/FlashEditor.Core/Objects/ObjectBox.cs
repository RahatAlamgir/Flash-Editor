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
