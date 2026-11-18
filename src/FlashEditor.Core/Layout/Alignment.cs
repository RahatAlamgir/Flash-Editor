using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Layout;

public enum AlignKind { Left, CenterHorizontally, Right, Top, CenterVertically, Bottom }

public enum DistributeKind { Horizontally, Vertically }

/// <summary>Works out how far each object has to move to line up or to be spread evenly. Pure arithmetic, no document needed.</summary>
public static class Alignment
{
    /// <summary>
    /// How far each of the <paramref name="boxes"/> has to move so that they line up. One box lines up with the canvas; several boxes
    /// line up with the area they cover together (the way design tools do it).
    /// </summary>
    public static (int Dx, int Dy)[] Align(IReadOnlyList<PixelRect> boxes, PixelRect canvas, AlignKind kind)
    {
        var result = new (int, int)[boxes.Count];
        if (boxes.Count == 0) return result;
        var area = boxes.Count == 1 ? canvas : Union(boxes);

        for (int i = 0; i < boxes.Count; i++)
        {
            var b = boxes[i];
            if (b.IsEmpty) continue;
            result[i] = kind switch
            {
                AlignKind.Left => (area.X - b.X, 0),
                AlignKind.Right => (area.Right - b.Right, 0),
                AlignKind.CenterHorizontally => (Middle(area.X, area.Right) - Middle(b.X, b.Right), 0),
                AlignKind.Top => (0, area.Y - b.Y),
                AlignKind.Bottom => (0, area.Bottom - b.Bottom),
                _ => (0, Middle(area.Y, area.Bottom) - Middle(b.Y, b.Bottom)),
            };
        }
        return result;
    }

    /// <summary>Lines up along both directions at once (one corner or the middle of the canvas, or of the area the boxes cover together).</summary>
    public static (int Dx, int Dy)[] AlignBoth(IReadOnlyList<PixelRect> boxes, PixelRect canvas, AlignKind horizontal, AlignKind vertical)
    {
        var across = Align(boxes, canvas, horizontal);
        var down = Align(boxes, canvas, vertical);
        var result = new (int, int)[boxes.Count];
        for (int i = 0; i < result.Length; i++) result[i] = (across[i].Dx, down[i].Dy);
        return result;
    }

    /// <summary>
    /// How far each box has to move so that the gaps between neighbouring boxes are equal. The two outer boxes stay where they are.
    /// Needs at least three boxes (otherwise nothing moves).
    /// </summary>
    public static (int Dx, int Dy)[] Distribute(IReadOnlyList<PixelRect> boxes, DistributeKind kind)
    {
        var result = new (int, int)[boxes.Count];
        var order = Enumerable.Range(0, boxes.Count).Where(i => !boxes[i].IsEmpty)
            .OrderBy(i => kind == DistributeKind.Horizontally ? boxes[i].X : boxes[i].Y).ToList();
        if (order.Count < 3) return result;

        bool horizontal = kind == DistributeKind.Horizontally;
        int Start(PixelRect b) => horizontal ? b.X : b.Y;
        int End(PixelRect b) => horizontal ? b.Right : b.Bottom;

        int first = Start(boxes[order[0]]), last = End(boxes[order[^1]]);
        int total = order.Sum(i => End(boxes[i]) - Start(boxes[i]));
        double gap = (last - first - total) / (double)(order.Count - 1);

        double position = first;
        foreach (int i in order)
        {
            int size = End(boxes[i]) - Start(boxes[i]);
            int move = (int)Math.Round(position) - Start(boxes[i]);
            result[i] = horizontal ? (move, 0) : (0, move);
            position += size + gap;
        }
        return result;
    }

    private static int Middle(int from, int to) => (int)Math.Round((from + to) / 2.0, MidpointRounding.AwayFromZero);

    private static PixelRect Union(IReadOnlyList<PixelRect> boxes)
    {
        var area = PixelRect.Empty;
        foreach (var b in boxes) if (!b.IsEmpty) area = area.Union(b);
        return area;
    }
}
