using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>One color of a gradient and where it sits along it (0 = start, 1 = end).</summary>
public sealed record ColorStop(ColorRgba Color, double Position);

/// <summary>
/// A color that fades through two or more colors, used instead of a flat color for the fill of a shape or the color of a text.
/// The direction is in the object's own space: turning or mirroring the object turns or mirrors its gradient with it.
/// </summary>
/// <param name="Stops">The colors and their places along the gradient (at least two, at most <see cref="MaxStops"/>). Their order in the list does not matter.</param>
/// <param name="Angle">Direction of a straight gradient in degrees: 0 = left to right, 90 = top to bottom, 180 = right to left, -90 = bottom to top.</param>
/// <param name="Radial">True: the gradient goes from the middle of the object outward instead of along a line.</param>
public sealed record GradientStyle(IReadOnlyList<ColorStop> Stops, double Angle = 90, bool Radial = false)
{
    public const int MaxStops = 8;
    public const int MinStops = 2;

    /// <summary>A gradient from one color to another.</summary>
    public static GradientStyle Two(ColorRgba from, ColorRgba to, double angle = 90, bool radial = false)
        => new(new[] { new ColorStop(from, 0), new ColorStop(to, 1) }, angle, radial);

    /// <summary>Colors spread evenly from start to end.</summary>
    public static GradientStyle Evenly(IReadOnlyList<ColorRgba> colors, double angle = 90, bool radial = false)
        => new(colors.Select((c, i) => new ColorStop(c, colors.Count == 1 ? 0 : (double)i / (colors.Count - 1))).ToArray(), angle, radial);

    /// <summary>The stops from start to end.</summary>
    public IReadOnlyList<ColorStop> Sorted => Stops.OrderBy(s => s.Position).ToArray();

    /// <summary>The first and the last color along the gradient.</summary>
    public ColorRgba From => Sorted[0].Color;
    public ColorRgba To => Sorted[^1].Color;

    // Two gradients are the same when they have the same colors in the same places (a list is compared by its content, not by identity).
    public bool Equals(GradientStyle? other)
        => other != null && Angle == other.Angle && Radial == other.Radial && Stops.SequenceEqual(other.Stops);

    public override int GetHashCode() => HashCode.Combine(Angle, Radial, Stops.Count);

    /// <summary>A pleasant second color for <paramref name="start"/>: the same kind of color with the hue turned a little and a bit lighter.</summary>
    public static GradientStyle Starting(ColorRgba start)
    {
        ColorMath.RgbToHsv(start.R / 255.0, start.G / 255.0, start.B / 255.0, out double h, out double s, out double v);
        // Grey, white and black colors have no hue to turn: fade to a lighter or darker version instead.
        if (s < 0.08) return Two(start, v > 0.5 ? new ColorRgba(80, 80, 80, start.A) : new ColorRgba(220, 220, 220, start.A));
        ColorMath.HsvToRgb(h + 45, Math.Min(1, s), Math.Min(1, v * 1.05 + 0.08), out double r, out double g, out double b);
        return Two(start, new ColorRgba(ColorMath.ClampByte(r * 255), ColorMath.ClampByte(g * 255), ColorMath.ClampByte(b * 255), start.A));
    }

    /// <summary>The gradient running the other way (colors keep their order but start from the other end).</summary>
    public GradientStyle Reversed() => this with { Stops = Stops.Select(s => s with { Position = 1 - s.Position }).ToArray() };

    /// <summary>The color of the gradient at a place along it (0..1), mixing the two stops around it.</summary>
    public ColorRgba ColorAt(double position)
    {
        var sorted = Sorted;
        if (position <= sorted[0].Position) return sorted[0].Color;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (position > sorted[i].Position) continue;
            var a = sorted[i - 1]; var b = sorted[i];
            double span = b.Position - a.Position;
            double t = span <= 1e-9 ? 1 : (position - a.Position) / span;
            return new ColorRgba(Mix(a.Color.R, b.Color.R, t), Mix(a.Color.G, b.Color.G, t), Mix(a.Color.B, b.Color.B, t), Mix(a.Color.A, b.Color.A, t));
        }
        return sorted[^1].Color;
    }

    private static byte Mix(byte a, byte b, double t) => ColorMath.ClampByte(a + (b - a) * t);

    /// <summary>One more color, put in the biggest gap between the existing ones, with the color the gradient has there. Unchanged at the maximum.</summary>
    public GradientStyle WithStopAdded()
    {
        if (Stops.Count >= MaxStops) return this;
        var sorted = Sorted;
        double bestStart = 0, bestEnd = 1, bestGap = -1;
        // The gaps: before the first stop, between stops, after the last stop.
        var edges = new List<double> { 0 };
        edges.AddRange(sorted.Select(s => s.Position));
        edges.Add(1);
        for (int i = 1; i < edges.Count; i++)
        {
            double gap = edges[i] - edges[i - 1];
            if (gap > bestGap) { bestGap = gap; bestStart = edges[i - 1]; bestEnd = edges[i]; }
        }
        double position = (bestStart + bestEnd) / 2;
        return this with { Stops = Stops.Append(new ColorStop(ColorAt(position), Math.Round(position, 3))).ToArray() };
    }

    /// <summary>The gradient without stop number <paramref name="index"/> (a position in <see cref="Stops"/>). Unchanged when only two are left.</summary>
    public GradientStyle WithStopRemoved(int index)
        => Stops.Count <= MinStops || index < 0 || index >= Stops.Count ? this : this with { Stops = Stops.Where((_, i) => i != index).ToArray() };

    /// <summary>The gradient with one stop changed.</summary>
    public GradientStyle WithStop(int index, Func<ColorStop, ColorStop> change)
        => index < 0 || index >= Stops.Count ? this : this with { Stops = Stops.Select((s, i) => i == index ? change(s) : s).ToArray() };
}

/// <summary>A ready-made gradient (one click in the Properties panel).</summary>
public sealed record GradientPreset(string Name, IReadOnlyList<ColorRgba> Colors, double Angle = 90, bool Radial = false)
{
    private static GradientPreset Make(string name, double angle, bool radial, params ColorRgba[] colors) => new(name, colors, angle, radial);

    public static GradientPreset Sunset { get; } = Make("Sunset", 45, false, new ColorRgba(255, 94, 98), new ColorRgba(255, 195, 113));
    public static GradientPreset Ocean { get; } = Make("Ocean", 90, false, new ColorRgba(0, 198, 251), new ColorRgba(0, 91, 234));
    public static GradientPreset Forest { get; } = Make("Forest", 90, false, new ColorRgba(86, 171, 47), new ColorRgba(19, 78, 94));
    public static GradientPreset Candy { get; } = Make("Candy", 0, false, new ColorRgba(255, 106, 213), new ColorRgba(147, 112, 255));
    public static GradientPreset Fire { get; } = Make("Fire", -90, false, new ColorRgba(255, 214, 0), new ColorRgba(220, 20, 20));
    public static GradientPreset Night { get; } = Make("Night", 90, false, new ColorRgba(67, 67, 160), new ColorRgba(12, 12, 40));
    public static GradientPreset Gold { get; } = Make("Gold", 45, false, new ColorRgba(255, 236, 160), new ColorRgba(191, 130, 20));
    public static GradientPreset Glow { get; } = Make("Glow", 0, true, new ColorRgba(255, 255, 255), new ColorRgba(59, 130, 246));
    public static GradientPreset Rainbow { get; } = Make("Rainbow", 0, false,
        new ColorRgba(239, 68, 68), new ColorRgba(250, 204, 21), new ColorRgba(34, 197, 94), new ColorRgba(59, 130, 246), new ColorRgba(168, 85, 247));
    public static GradientPreset Aurora { get; } = Make("Aurora", 60, false, new ColorRgba(16, 185, 129), new ColorRgba(59, 130, 246), new ColorRgba(168, 85, 247));
    public static GradientPreset Sunrise { get; } = Make("Sunrise", -90, false, new ColorRgba(30, 27, 75), new ColorRgba(236, 72, 153), new ColorRgba(251, 191, 36));

    public static IReadOnlyList<GradientPreset> All { get; } = new[] { Sunset, Ocean, Forest, Candy, Fire, Night, Gold, Glow, Rainbow, Aurora, Sunrise };

    public GradientStyle CreateStyle() => GradientStyle.Evenly(Colors, Angle, Radial);
}
