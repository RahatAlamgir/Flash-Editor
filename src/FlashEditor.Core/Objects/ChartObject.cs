using System.Globalization;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>What kind of chart is drawn.</summary>
public enum ChartKind { Columns, Bars, Line, Pie, Donut }

/// <summary>One value of a chart with its name.</summary>
public sealed record ChartItem(string Label, double Value);

/// <summary>
/// A chart that stays editable: change the numbers, the kind, the colors and what is shown at any time. It is drawn as vectors, so it stays
/// sharp at any size.
/// </summary>
/// <param name="Items">The values, in the order they are drawn.</param>
/// <param name="Colors">The colors of the bars / slices (used in turn). A line chart uses the first one.</param>
/// <param name="BarWidth">How much of its space a bar takes (0.2 = thin bars, 1 = bars touching).</param>
/// <param name="ValuePrefix">Written before every number, for example "$".</param>
/// <param name="ValueSuffix">Written after every number, for example "%" or " kg".</param>
/// <param name="TextScale">Size of all text in the chart (1 = normal).</param>
public sealed record ChartObject(
    ObjectBox Box,
    ChartKind Kind,
    IReadOnlyList<ChartItem> Items,
    IReadOnlyList<ColorRgba> Colors,
    string Title = "",
    bool ShowValues = true,
    bool ShowLabels = true,
    bool ShowGrid = true,
    ColorRgba? Background = null,
    ColorRgba? TextColor = null,
    bool FlipX = false,
    bool FlipY = false,
    double BarWidth = 0.62,
    string ValuePrefix = "",
    string ValueSuffix = "",
    double TextScale = 1.0) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Chart";
    public override string SuggestedLayerName => string.IsNullOrWhiteSpace(Title) ? "Chart" : Title.Length > 24 ? Title[..24] + "..." : Title;
    public override bool KeepsAspectByDefault => false;

    /// <summary>The color of the text in the chart (dark slate when nothing was chosen).</summary>
    public ColorRgba Ink => TextColor ?? new ColorRgba(30, 41, 59);

    /// <summary>The color of the <paramref name="index"/>-th bar or slice.</summary>
    public ColorRgba ColorOf(int index) => Colors.Count == 0 ? ChartPalettes.Ocean[index % ChartPalettes.Ocean.Count] : Colors[index % Colors.Count];

    /// <summary>A number as it is written in the chart (with the prefix and suffix).</summary>
    public string Format(double value)
        => ValuePrefix + (Math.Abs(value) >= 1000 ? value.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture) : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)) + ValueSuffix;

    /// <summary>A chart with sample numbers, to start from.</summary>
    public static ChartObject Sample(ObjectBox box, ChartKind kind = ChartKind.Columns) => new(box, kind,
        new[] { new ChartItem("Jan", 12), new ChartItem("Feb", 19), new ChartItem("Mar", 8), new ChartItem("Apr", 15), new ChartItem("May", 22) },
        ChartPalettes.Ocean, "Sales");
}

/// <summary>Ready-made color sets for charts.</summary>
public static class ChartPalettes
{
    private static ColorRgba C(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);

    public static IReadOnlyList<ColorRgba> Ocean { get; } = new[] { C(59, 130, 246), C(14, 165, 233), C(6, 182, 212), C(20, 184, 166), C(99, 102, 241), C(139, 92, 246) };
    public static IReadOnlyList<ColorRgba> Sunset { get; } = new[] { C(239, 68, 68), C(249, 115, 22), C(245, 158, 11), C(236, 72, 153), C(217, 70, 239), C(250, 204, 21) };
    public static IReadOnlyList<ColorRgba> Forest { get; } = new[] { C(22, 163, 74), C(132, 204, 22), C(13, 148, 136), C(101, 163, 13), C(5, 150, 105), C(163, 230, 53) };
    public static IReadOnlyList<ColorRgba> Candy { get; } = new[] { C(244, 114, 182), C(167, 139, 250), C(96, 165, 250), C(52, 211, 153), C(251, 191, 36), C(251, 113, 133) };
    public static IReadOnlyList<ColorRgba> Mono { get; } = new[] { C(30, 41, 59), C(71, 85, 105), C(100, 116, 139), C(148, 163, 184), C(203, 213, 225) };

    public static IReadOnlyList<(string Name, IReadOnlyList<ColorRgba> Colors)> All { get; } = new (string, IReadOnlyList<ColorRgba>)[]
    {
        ("Ocean", Ocean), ("Sunset", Sunset), ("Forest", Forest), ("Candy", Candy), ("Mono", Mono),
    };
}

/// <summary>Turns the text a user types ("Jan, 12" per line) into chart values and back.</summary>
public static class ChartData
{
    /// <summary>One item per line: a name and a number separated by a comma, semicolon, colon or tab. Lines without a number are skipped.</summary>
    public static IReadOnlyList<ChartItem> Parse(string text)
    {
        var items = new List<ChartItem>();
        foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            int cut = line.LastIndexOfAny(new[] { ',', ';', ':', '\t' });
            string label = cut < 0 ? "" : line[..cut].Trim();
            string number = (cut < 0 ? line : line[(cut + 1)..]).Trim().Replace(" ", "");
            if (cut < 0 && double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) label = "";
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value)) continue;
            items.Add(new ChartItem(label.Length == 0 ? (items.Count + 1).ToString(CultureInfo.InvariantCulture) : label, value));
            if (items.Count >= 60) break;
        }
        return items;
    }

    public static string Format(IReadOnlyList<ChartItem> items)
        => string.Join("\n", items.Select(i => $"{i.Label}, {i.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));
}
