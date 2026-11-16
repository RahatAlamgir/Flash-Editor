namespace FlashEditor.Core.Imaging;

/// <summary>An integer rectangle in pixel coordinates. Right/Bottom are exclusive.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public static readonly PixelRect Empty = new(0, 0, 0, 0);

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
    public long Area => IsEmpty ? 0 : (long)Width * Height;

    /// <summary>Builds the smallest rectangle containing both corner points (inclusive pixels).</summary>
    public static PixelRect FromCorners(int x1, int y1, int x2, int y2)
    {
        int left = Math.Min(x1, x2), top = Math.Min(y1, y2);
        int right = Math.Max(x1, x2), bottom = Math.Max(y1, y2);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>Rectangle covering the given floating-point bounds, rounded outwards.</summary>
    public static PixelRect FromBounds(double left, double top, double right, double bottom)
    {
        int l = (int)Math.Floor(left), t = (int)Math.Floor(top);
        int r = (int)Math.Ceiling(right), b = (int)Math.Ceiling(bottom);
        return new PixelRect(l, t, Math.Max(0, r - l), Math.Max(0, b - t));
    }

    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;

    public PixelRect Intersect(PixelRect other)
    {
        int l = Math.Max(X, other.X), t = Math.Max(Y, other.Y);
        int r = Math.Min(Right, other.Right), b = Math.Min(Bottom, other.Bottom);
        return r <= l || b <= t ? Empty : new PixelRect(l, t, r - l, b - t);
    }

    public PixelRect Union(PixelRect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        int l = Math.Min(X, other.X), t = Math.Min(Y, other.Y);
        int r = Math.Max(Right, other.Right), b = Math.Max(Bottom, other.Bottom);
        return new PixelRect(l, t, r - l, b - t);
    }

    public PixelRect Inflate(int amount)
        => new(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);

    public PixelRect Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };

    public override string ToString() => $"{X},{Y} {Width}x{Height}";
}
