namespace FlashEditor.Core.Documents;

/// <summary>A point of a tone curve: the brightness that goes in (X) and the brightness that comes out (Y), both 0..255.</summary>
public readonly record struct CurvePoint(double X, double Y);

/// <summary>The maths of Levels and Curves: tables that tell what each brightness 0..255 becomes.</summary>
public static class CurveMath
{
    /// <summary>The curve that changes nothing: from the dark end to the light end in a straight line.</summary>
    public static IReadOnlyList<CurvePoint> Straight { get; } = new[] { new CurvePoint(0, 0), new CurvePoint(255, 255) };

    /// <summary>True when the points make a straight line from (0, 0) to (255, 255) (or there are none).</summary>
    public static bool IsStraight(IReadOnlyList<CurvePoint>? points)
        => points == null || points.Count == 0 || (points.Count == 2 && points[0] == new CurvePoint(0, 0) && points[1] == new CurvePoint(255, 255));

    /// <summary>
    /// The table of a curve through the given points: 256 values, smooth (the curve never overshoots between points, so it stays
    /// natural looking). Without points it is a straight line.
    /// </summary>
    public static byte[] BuildTable(IReadOnlyList<CurvePoint>? points)
    {
        var table = new byte[256];
        if (IsStraight(points))
        {
            for (int i = 0; i < 256; i++) table[i] = (byte)i;
            return table;
        }

        // Sorted by X, one point per X, and always an anchor at both ends.
        var list = points!.OrderBy(p => p.X).GroupBy(p => Math.Round(p.X)).Select(g => new CurvePoint(Math.Round(g.Last().X), g.Last().Y)).ToList();
        if (list[0].X > 0) list.Insert(0, new CurvePoint(0, list[0].Y));
        if (list[^1].X < 255) list.Add(new CurvePoint(255, list[^1].Y));
        if (list.Count == 1) list.Add(new CurvePoint(list[0].X + 1, list[0].Y));

        int n = list.Count;
        var xs = list.Select(p => p.X).ToArray();
        var ys = list.Select(p => Math.Clamp(p.Y, 0, 255)).ToArray();

        // Monotone cubic interpolation (Fritsch-Carlson): smooth, and it only goes up and down where the points do.
        var delta = new double[n - 1];
        for (int i = 0; i < n - 1; i++) delta[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
        var m = new double[n];
        m[0] = delta[0];
        m[n - 1] = delta[n - 2];
        for (int i = 1; i < n - 1; i++) m[i] = delta[i - 1] * delta[i] <= 0 ? 0 : (delta[i - 1] + delta[i]) / 2;
        for (int i = 0; i < n - 1; i++)
        {
            if (delta[i] == 0) { m[i] = 0; m[i + 1] = 0; continue; }
            double a = m[i] / delta[i], b = m[i + 1] / delta[i];
            double s = a * a + b * b;
            if (s > 9)
            {
                double t = 3 / Math.Sqrt(s);
                m[i] = t * a * delta[i];
                m[i + 1] = t * b * delta[i];
            }
        }

        int seg = 0;
        for (int x = 0; x < 256; x++)
        {
            while (seg < n - 2 && x > xs[seg + 1]) seg++;
            double h = xs[seg + 1] - xs[seg];
            double t = (x - xs[seg]) / h;
            double t2 = t * t, t3 = t2 * t;
            double y = (2 * t3 - 3 * t2 + 1) * ys[seg] + (t3 - 2 * t2 + t) * h * m[seg] + (-2 * t3 + 3 * t2) * ys[seg + 1] + (t3 - t2) * h * m[seg + 1];
            table[x] = (byte)Math.Clamp((int)Math.Round(y), 0, 255);
        }
        return table;
    }

    /// <summary>
    /// The table of a Levels adjustment: everything darker than <paramref name="inBlack"/> becomes black, everything lighter than
    /// <paramref name="inWhite"/> becomes white, <paramref name="gamma"/> moves the middle tones (above 1 lighter, below 1 darker), and the
    /// result is squeezed between <paramref name="outBlack"/> and <paramref name="outWhite"/>.
    /// </summary>
    public static byte[] LevelsTable(double inBlack, double inWhite, double gamma, double outBlack, double outWhite)
    {
        var table = new byte[256];
        double range = Math.Max(1, inWhite - inBlack);
        double g = 1.0 / Math.Clamp(gamma, 0.05, 20);
        for (int i = 0; i < 256; i++)
        {
            double v = Math.Clamp((i - inBlack) / range, 0, 1);
            v = Math.Pow(v, g);
            table[i] = (byte)Math.Clamp((int)Math.Round(outBlack + v * (outWhite - outBlack)), 0, 255);
        }
        return table;
    }

    /// <summary>Puts <paramref name="point"/> into a curve (replacing a point that is close to it in X). Returns a new list, sorted by X.</summary>
    public static IReadOnlyList<CurvePoint> WithPoint(IReadOnlyList<CurvePoint>? points, CurvePoint point)
    {
        var list = (points == null || points.Count == 0 ? Straight : points).Where(p => Math.Abs(p.X - point.X) >= 4).ToList();
        list.Add(new CurvePoint(Math.Clamp(Math.Round(point.X), 0, 255), Math.Clamp(Math.Round(point.Y), 0, 255)));
        return list.OrderBy(p => p.X).ToList();
    }

    /// <summary>Takes a point out of a curve (the two end points stay, they only can be moved up and down).</summary>
    public static IReadOnlyList<CurvePoint> WithoutPoint(IReadOnlyList<CurvePoint> points, int index)
    {
        if (index <= 0 || index >= points.Count - 1) return points;
        return points.Where((_, i) => i != index).ToList();
    }
}
