using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Snapping;

/// <summary>What things snap to.</summary>
/// <param name="Objects">The edges and centers of the other layers.</param>
/// <param name="Grid">The lines of the grid.</param>
/// <param name="Canvas">The edges and the center of the canvas.</param>
/// <param name="Guides">The guide lines the user pulled out of the rulers.</param>
/// <param name="GridSize">Distance between grid lines in pixels.</param>
/// <param name="Spacing">Equal gaps: the dragged object keeps the same distance to its neighbors as the other objects have between them.</param>
public sealed record SnapSettings(bool Objects, bool Grid, bool Canvas, bool Guides, double GridSize = 50, bool Spacing = true);

/// <summary>
/// A gap that is shown while it matches another gap (equal spacing): a measured stretch of <see cref="Value"/> pixels.
/// A horizontal gap runs from x = <see cref="From"/> to <see cref="To"/> at y = <see cref="Cross"/>; a vertical one the other way round.
/// </summary>
public readonly record struct GapMark(bool Horizontal, double From, double To, double Cross, double Value);

/// <summary>
/// How far to move something so it lines up, and which lines / gaps it lined up with (so they can be drawn as smart guides).
/// <see cref="Dx"/> / <see cref="Dy"/> are 0 when nothing was close enough.
/// </summary>
public readonly record struct SnapResult(double Dx, double Dy, IReadOnlyList<double> LinesX, IReadOnlyList<double> LinesY,
    IReadOnlyList<GapMark>? GapList = null)
{
    public static SnapResult None { get; } = new(0, 0, Array.Empty<double>(), Array.Empty<double>());

    /// <summary>The gaps that are equal (empty when none).</summary>
    public IReadOnlyList<GapMark> Gaps => GapList ?? Array.Empty<GapMark>();

    public bool Snapped => Dx != 0 || Dy != 0 || LinesX.Count > 0 || LinesY.Count > 0 || Gaps.Count > 0;
}

/// <summary>
/// Snapping while something is dragged. Made once when the drag starts (it looks at all layers then), and asked for every mouse
/// move: "if this box / point is near a line, or would make equal gaps, how far should it move?".
/// </summary>
public sealed class SnapSession
{
    private readonly record struct Box(double Left, double Top, double Right, double Bottom);

    private readonly List<double> _xs = new(), _ys = new();   // vertical / horizontal lines of objects, canvas and guides
    private readonly List<Box> _boxes = new();                 // the other objects, for equal spacing
    private readonly SnapSettings _settings;

    /// <param name="exclude">Layers being dragged (they do not snap to themselves).</param>
    public SnapSession(Document document, IEnumerable<Layer> exclude, IReadOnlyList<double> guidesX, IReadOnlyList<double> guidesY, SnapSettings settings)
    {
        _settings = settings;
        var skip = new HashSet<Layer>(exclude);

        if (settings.Objects || settings.Spacing)
        {
            foreach (var layer in document.Layers)
            {
                if (skip.Contains(layer) || !layer.IsVisible || layer.Opacity <= 0.02) continue;
                var bounds = layer.Object != null ? layer.Object.VisualBounds() : layer.VisibleBounds();
                if (bounds.IsEmpty) continue;
                if (layer.Object == null && bounds.Width >= document.Width && bounds.Height >= document.Height) continue; // a background
                if (settings.Objects) AddBox(bounds.X, bounds.Y, bounds.Right, bounds.Bottom);
                _boxes.Add(new Box(bounds.X, bounds.Y, bounds.Right, bounds.Bottom));
            }
        }
        if (settings.Canvas)
        {
            _xs.AddRange(new[] { 0.0, document.Width / 2.0, document.Width });
            _ys.AddRange(new[] { 0.0, document.Height / 2.0, document.Height });
        }
        if (settings.Guides)
        {
            _xs.AddRange(guidesX);
            _ys.AddRange(guidesY);
        }
    }

    private void AddBox(double l, double t, double r, double b)
    {
        _xs.AddRange(new[] { l, (l + r) / 2, r });
        _ys.AddRange(new[] { t, (t + b) / 2, b });
    }

    /// <summary>
    /// A rectangle (left, top, right, bottom): its edges and its center snap, whichever is nearest to a line within
    /// <paramref name="threshold"/> pixels; it also snaps so the gaps to its neighbors are equal to gaps between other objects.
    /// </summary>
    public SnapResult SnapBox(double left, double top, double right, double bottom, double threshold)
    {
        bool lineX = Best(new[] { left, (left + right) / 2, right }, _xs, threshold, out var dxLine, out var matchedX);
        bool lineY = Best(new[] { top, (top + bottom) / 2, bottom }, _ys, threshold, out var dyLine, out var matchedY);

        var gaps = new List<GapMark>();
        double? dxGap = null, dyGap = null;
        if (_settings.Spacing)
        {
            dxGap = EqualSpacing(horizontal: true, left, right, top, bottom, threshold, gaps);
            dyGap = EqualSpacing(horizontal: false, top, bottom, left, right, threshold, gaps);
        }

        // Per direction the nearest of "a line" and "equal gaps" wins; when both agree both are shown.
        double dx = Pick(lineX, dxLine, dxGap, out bool useLineX, out bool useGapX);
        double dy = Pick(lineY, dyLine, dyGap, out bool useLineY, out bool useGapY);
        gaps.RemoveAll(g => g.Horizontal ? !useGapX : !useGapY);

        var xs = useLineX && matchedX is { } mx ? new[] { mx } : Array.Empty<double>();
        var ys = useLineY && matchedY is { } my ? new[] { my } : Array.Empty<double>();
        return new SnapResult(dx, dy, xs, ys, gaps);
    }

    /// <summary>A single point (a mouse position while drawing a shape or dragging a corner).</summary>
    public SnapResult SnapPoint(PointD point, double threshold)
    {
        Best(new[] { point.X }, _xs, threshold, out var dx, out var lineX);
        Best(new[] { point.Y }, _ys, threshold, out var dy, out var lineY);
        return new SnapResult(dx, dy, lineX is { } x ? new[] { x } : Array.Empty<double>(), lineY is { } y ? new[] { y } : Array.Empty<double>());
    }

    private static double Pick(bool line, double lineDelta, double? gapDelta, out bool useLine, out bool useGap)
    {
        useLine = line;
        useGap = gapDelta != null;
        if (line && gapDelta is { } gap)
        {
            if (Math.Abs(gap - lineDelta) <= 0.5) return lineDelta;       // both agree: show both
            if (Math.Abs(gap) < Math.Abs(lineDelta)) { useLine = false; return gap; }
            useGap = false;
            return lineDelta;
        }
        return line ? lineDelta : gapDelta ?? 0;
    }

    /// <summary>The smallest move (within the threshold) that puts one of the values on a line or a grid line; false when none is close.</summary>
    private bool Best(IReadOnlyList<double> values, List<double> lines, double threshold, out double delta, out double? matchedLine)
    {
        double best = double.MaxValue, bestLine = 0;
        bool onLine = false;
        foreach (var value in values)
        {
            foreach (var line in lines)
            {
                double d = line - value;
                if (Math.Abs(d) <= threshold && Math.Abs(d) < Math.Abs(best)) { best = d; bestLine = line; onLine = true; }
            }
        }
        if (_settings.Grid && _settings.GridSize >= 1)
        {
            foreach (var value in values)
            {
                double d = Math.Round(value / _settings.GridSize) * _settings.GridSize - value;
                if (Math.Abs(d) <= threshold && Math.Abs(d) < Math.Abs(best)) { best = d; onLine = false; }
            }
        }
        matchedLine = onLine ? bestLine : null;
        delta = best == double.MaxValue ? 0 : best;
        return best != double.MaxValue;
    }

    // ------------------------------------------------------------------ equal spacing

    /// <summary>
    /// Looks along one direction (horizontal = left/right) at the other objects that share a row (their vertical ranges overlap
    /// the dragged box). The dragged box snaps when it would
    /// <list type="bullet">
    /// <item>sit in the middle of a gap between two objects (equal gaps on both sides), or</item>
    /// <item>sit next to an object at the same distance two other objects have between them.</item>
    /// </list>
    /// Returns the move along the direction, or null; the matching gaps are added to <paramref name="marks"/>.
    /// </summary>
    private double? EqualSpacing(bool horizontal, double start, double end, double crossStart, double crossEnd, double threshold, List<GapMark> marks)
    {
        // The other objects in the same row (or column), left to right, as ranges along the direction.
        var row = _boxes
            .Where(b => (horizontal ? b.Top < crossEnd && b.Bottom > crossStart : b.Left < crossEnd && b.Right > crossStart))
            .Select(b => horizontal ? (A0: b.Left, A1: b.Right, C0: b.Top, C1: b.Bottom) : (A0: b.Top, A1: b.Bottom, C0: b.Left, C1: b.Right))
            .OrderBy(b => b.A0)
            .ToList();
        if (row.Count == 0) return null;

        double size = end - start, crossMid = (crossStart + crossEnd) / 2;
        var candidates = new List<(double Delta, List<GapMark> Marks)>();

        GapMark Mark(double from, double to, double cross) => new(horizontal, from, to, cross, Math.Round(to - from));
        double RowCross((double A0, double A1, double C0, double C1) a, (double A0, double A1, double C0, double C1) b)
            => (Math.Max(a.C0, b.C0) + Math.Min(a.C1, b.C1)) / 2;

        // 1) In the middle between two neighbors.
        for (int i = 0; i + 1 < row.Count; i++)
        {
            var a = row[i];
            var b = row[i + 1];
            double room = b.A0 - a.A1;
            if (room < size) continue;
            double gap = (room - size) / 2, target = a.A1 + gap;
            double delta = target - start;
            if (Math.Abs(delta) <= threshold)
                candidates.Add((delta, new List<GapMark> { Mark(a.A1, target, crossMid), Mark(target + size, b.A0, crossMid) }));
        }

        // 2) Next to an object, at a distance that two other objects already have between them.
        for (int i = 0; i + 1 < row.Count; i++)
        {
            double g = row[i + 1].A0 - row[i].A1;
            if (g <= 0) continue;
            var reference = Mark(row[i].A1, row[i + 1].A0, RowCross(row[i], row[i + 1]));
            for (int j = 0; j < row.Count; j++)
            {
                var x = row[j];
                // To the right of x:
                double rightStart = x.A1 + g, d1 = rightStart - start;
                if (Math.Abs(d1) <= threshold && rightStart >= x.A1)
                    candidates.Add((d1, new List<GapMark> { reference, Mark(x.A1, rightStart, crossMid) }));
                // To the left of x:
                double leftEnd = x.A0 - g, d2 = leftEnd - end;
                if (Math.Abs(d2) <= threshold)
                    candidates.Add((d2, new List<GapMark> { reference, Mark(leftEnd, x.A0, crossMid) }));
            }
        }
        if (candidates.Count == 0) return null;

        double best = candidates.OrderBy(c => Math.Abs(c.Delta)).First().Delta;
        foreach (var c in candidates)
            if (Math.Abs(c.Delta - best) <= 0.5)
                foreach (var m in c.Marks)
                    if (!marks.Contains(m)) marks.Add(m);
        return best;
    }
}
