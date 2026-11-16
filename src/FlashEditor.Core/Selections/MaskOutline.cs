using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Selections;

/// <summary>
/// Traces the outline of a pixel mask into closed polygons (along pixel edges), so any mask
/// (magic wand, AI subject, combined selections) can be shown with marching ants.
/// Holes come out as separate loops; drawn with the even-odd rule they look right.
/// </summary>
public static class MaskOutline
{
    public static IReadOnlyList<IReadOnlyList<PointD>> Trace(byte[] mask, int width, int height, byte threshold = 128)
    {
        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && mask[y * width + x] >= threshold;

        // Directed boundary edges between grid corners, keeping the inside on the right-hand side.
        var edges = new Dictionary<long, List<long>>();
        long Key(int x, int y) => ((long)y << 32) | (uint)x;
        void AddEdge(int x1, int y1, int x2, int y2)
        {
            long from = Key(x1, y1);
            if (!edges.TryGetValue(from, out var list)) edges[from] = list = new List<long>(1);
            list.Add(Key(x2, y2));
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!Inside(x, y)) continue;
                if (!Inside(x, y - 1)) AddEdge(x, y, x + 1, y);             // top, left to right
                if (!Inside(x + 1, y)) AddEdge(x + 1, y, x + 1, y + 1);     // right, downwards
                if (!Inside(x, y + 1)) AddEdge(x + 1, y + 1, x, y + 1);     // bottom, right to left
                if (!Inside(x - 1, y)) AddEdge(x, y + 1, x, y);             // left, upwards
            }
        }

        var loops = new List<IReadOnlyList<PointD>>();
        while (edges.Count > 0)
        {
            long start = edges.Keys.First();
            var points = new List<PointD>();
            long current = start;
            while (edges.TryGetValue(current, out var nexts))
            {
                long next = nexts[^1];
                nexts.RemoveAt(nexts.Count - 1);
                if (nexts.Count == 0) edges.Remove(current);
                points.Add(new PointD((int)(current & 0xFFFFFFFF), (int)(current >> 32)));
                current = next;
                if (current == start) break;
            }
            var simplified = RemoveStraightPoints(points);
            if (simplified.Count >= 3) loops.Add(simplified);
        }
        return loops;
    }

    /// <summary>Drops points that lie on a straight line between their neighbors.</summary>
    private static List<PointD> RemoveStraightPoints(List<PointD> points)
    {
        if (points.Count < 4) return points;
        var result = new List<PointD>(points.Count / 2);
        for (int i = 0; i < points.Count; i++)
        {
            var prev = points[(i - 1 + points.Count) % points.Count];
            var p = points[i];
            var next = points[(i + 1) % points.Count];
            bool straight = (prev.X == p.X && p.X == next.X) || (prev.Y == p.Y && p.Y == next.Y);
            if (!straight) result.Add(p);
        }
        return result;
    }
}
