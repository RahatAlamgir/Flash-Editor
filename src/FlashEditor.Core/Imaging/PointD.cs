namespace FlashEditor.Core.Imaging;

/// <summary>A point with floating-point coordinates (image space).</summary>
public readonly record struct PointD(double X, double Y)
{
    public double DistanceTo(PointD other)
    {
        double dx = other.X - X, dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public PointD Offset(double dx, double dy) => new(X + dx, Y + dy);
}
