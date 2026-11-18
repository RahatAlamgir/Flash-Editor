using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>How a bent line curves.</summary>
public enum LineBendStyle { Arc, Wave }

/// <summary>
/// The shape of a bent line, in the box of the line (0,0 = top-left; the line runs from the left end to the right end through the middle of the box).
/// A bent line is one cubic Bezier curve: an arc bows to one side, a wave goes up and then down.
/// </summary>
public static class LineCurves
{
    /// <summary>The four control points of the line (start, two handles, end). With no bend the handles lie on the ends (a straight line).</summary>
    /// <param name="bend">-1 .. 1: how far the line bows, as a fraction of its length (negative = the other side).</param>
    public static (PointD Start, PointD Handle1, PointD Handle2, PointD End) Controls(double width, double height, double bend, LineBendStyle style)
    {
        var start = new PointD(0, height / 2);
        var end = new PointD(width, height / 2);
        bend = Math.Clamp(bend, -1, 1);
        if (Math.Abs(bend) < 1e-6) return (start, start, end, end);

        double y = height / 2;
        if (style == LineBendStyle.Wave)
        {
            double offset = bend * width * 0.45;
            return (start, new PointD(width / 3, y + offset), new PointD(width * 2 / 3, y - offset), end);
        }
        // An arc: a quadratic curve with its middle handle at bend * length / 2 from the line, written as a cubic.
        double control = y + bend * width * 0.5;
        return (start, new PointD(width / 3, y + (control - y) * 2 / 3), new PointD(width * 2 / 3, y + (control - y) * 2 / 3), end);
    }

    /// <summary>How far the bent line reaches away from the straight line, in pixels (the room the drawing needs around the box).</summary>
    public static double Reach(double width, double bend, LineBendStyle style)
        => Math.Abs(Math.Clamp(bend, -1, 1)) * width * (style == LineBendStyle.Wave ? 0.3 : 0.26);

    /// <summary>Where the line leaves its first end and enters its last end: unit vectors pointing out of the line at the two tips (for arrow heads).</summary>
    public static (PointD StartOutward, PointD EndOutward) TipDirections(PointD start, PointD handle1, PointD handle2, PointD end)
    {
        var a = Unit(start.X - (handle1 != start ? handle1.X : handle2 != end ? handle2.X : end.X), start.Y - (handle1 != start ? handle1.Y : handle2 != end ? handle2.Y : end.Y));
        var b = Unit(end.X - (handle2 != end ? handle2.X : handle1 != start ? handle1.X : start.X), end.Y - (handle2 != end ? handle2.Y : handle1 != start ? handle1.Y : start.Y));
        return (a, b);
    }

    private static PointD Unit(double x, double y)
    {
        double d = Math.Sqrt(x * x + y * y);
        return d < 1e-9 ? new PointD(1, 0) : new PointD(x / d, y / d);
    }
}
