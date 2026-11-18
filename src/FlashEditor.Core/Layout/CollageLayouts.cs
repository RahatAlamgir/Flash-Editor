namespace FlashEditor.Core.Layout;

/// <summary>One frame of a collage: where it sits and how many grid cells it covers.</summary>
public readonly record struct CollageCell(int Column, int Row, int ColumnSpan = 1, int RowSpan = 1);

/// <summary>A ready-made arrangement of frames on a grid (like the layouts of a collage maker).</summary>
public sealed record CollageLayout(string Name, int Columns, int Rows, IReadOnlyList<CollageCell> Cells)
{
    /// <summary>The frames as rectangles on a canvas: an even border (<paramref name="padding"/>) and the same gap between the frames.</summary>
    public IReadOnlyList<(double X, double Y, double Width, double Height)> Place(double canvasWidth, double canvasHeight, double padding, double gap)
    {
        double cellW = Math.Max(1, (canvasWidth - 2 * padding - gap * (Columns - 1)) / Columns);
        double cellH = Math.Max(1, (canvasHeight - 2 * padding - gap * (Rows - 1)) / Rows);
        return Cells.Select(c => (
            padding + c.Column * (cellW + gap),
            padding + c.Row * (cellH + gap),
            c.ColumnSpan * cellW + (c.ColumnSpan - 1) * gap,
            c.RowSpan * cellH + (c.RowSpan - 1) * gap)).ToList();
    }

    private static IReadOnlyList<CollageCell> Grid(int columns, int rows)
        => Enumerable.Range(0, rows).SelectMany(r => Enumerable.Range(0, columns).Select(c => new CollageCell(c, r))).ToList();

    public static IReadOnlyList<CollageLayout> All { get; } = new[]
    {
        new CollageLayout("Two side by side", 2, 1, Grid(2, 1)),
        new CollageLayout("Two stacked", 1, 2, Grid(1, 2)),
        new CollageLayout("Three columns", 3, 1, Grid(3, 1)),
        new CollageLayout("Three rows", 1, 3, Grid(1, 3)),
        new CollageLayout("2 x 2 grid", 2, 2, Grid(2, 2)),
        new CollageLayout("3 x 3 grid", 3, 3, Grid(3, 3)),
        new CollageLayout("One large, two small", 2, 2, new[] { new CollageCell(0, 0, 1, 2), new CollageCell(1, 0), new CollageCell(1, 1) }),
        new CollageLayout("Large on top, three small", 3, 3, new[] { new CollageCell(0, 0, 3, 2), new CollageCell(0, 2), new CollageCell(1, 2), new CollageCell(2, 2) }),
        new CollageLayout("Large on the left, three small", 3, 3, new[] { new CollageCell(0, 0, 2, 3), new CollageCell(2, 0), new CollageCell(2, 1), new CollageCell(2, 2) }),
        new CollageLayout("Wide band and four", 2, 3, new[] { new CollageCell(0, 0, 2, 1), new CollageCell(0, 1), new CollageCell(1, 1), new CollageCell(0, 2), new CollageCell(1, 2) }),
    };
}
