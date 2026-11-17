using FlashEditor.AI.Models;
using FlashEditor.AI.Runtime;
using FlashEditor.Core.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FlashEditor.AI.Inpainting;

/// <summary>
/// Removes things from a picture: fills the selected area with what would plausibly be there (the LaMa model). The model is loaded on first
/// use and kept in memory. It runs on the processor: this model does not run correctly on the graphics card through DirectML.
/// </summary>
public sealed class Inpainter : IDisposable
{
    private readonly string _modelPath;
    private readonly int _modelSize;
    private readonly object _lock = new();
    private InferenceSession? _session;

    public Inpainter(ModelInfo model, string modelPath)
    {
        _modelPath = modelPath;
        _modelSize = model.InputSize;
    }

    /// <summary>What the model runs on (empty until first used).</summary>
    public string DeviceName { get; private set; } = "";

    /// <summary>The longest side of a selection that is filled in one go (at full detail); bigger ones are filled piece by piece.</summary>
    public const int SingleFillMax = 340;

    /// <summary>The size of one piece when a big selection is filled piece by piece.</summary>
    public const int PieceSide = 256;

    /// <summary>
    /// Fills the area of <paramref name="image"/> where <paramref name="mask"/> (0..255, picture sized) is set. Returns a picture of the same
    /// size that holds only the filled part (see-through everywhere else), or null when the mask is empty.
    /// <para>
    /// The model works on 512 x 512 pixels. A big selection shown to it whole would have to be shrunk and enlarged again, which makes the fill
    /// soft and smooth. So a big selection is filled in pieces: every piece is seen at full detail, starting where most of the surroundings are
    /// known, and each next piece also sees what was filled before.
    /// </para>
    /// </summary>
    /// <param name="progress">Receives 0..1 while the pieces are done.</param>
    public PixelBuffer? Fill(PixelBuffer image, byte[] mask, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        if (mask.Length != image.Width * image.Height) throw new ArgumentException("The mask must be as big as the picture.", nameof(mask));
        var area = BoundsOf(mask, image.Width, image.Height);
        if (area.IsEmpty) return null;

        if (Math.Max(area.Width, area.Height) <= SingleFillMax)
        {
            var one = FillPiece(image, mask, mask, area, cancellation);
            if (one != null) MatchDetail(one, image, mask, area);
            progress?.Report(1);
            return one;
        }

        // Cut the selection into pieces and fill them one after another.
        var pieces = new List<PixelRect>();
        for (int y = area.Y; y < area.Bottom; y += PieceSide)
            for (int x = area.X; x < area.Right; x += PieceSide)
            {
                var rect = new PixelRect(x, y, Math.Min(PieceSide, area.Right - x), Math.Min(PieceSide, area.Bottom - y));
                if (AnySet(mask, image.Width, rect)) pieces.Add(rect);
            }

        var work = image.Clone();                      // the picture as it gets filled
        var remaining = (byte[])mask.Clone();          // what is still empty
        var result = new PixelBuffer(image.Width, image.Height);
        int total = pieces.Count, done = 0;
        while (pieces.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var rect = pieces[BestPiece(pieces, remaining, image.Width, image.Height)];
            pieces.Remove(rect);

            var pieceMask = new byte[mask.Length];
            for (int y = rect.Y; y < rect.Bottom; y++)
                Array.Copy(remaining, y * image.Width + rect.X, pieceMask, y * image.Width + rect.X, rect.Width);
            var pieceArea = BoundsOf(pieceMask, image.Width, image.Height);
            if (!pieceArea.IsEmpty)
            {
                // The model must treat everything still empty as a hole, but only this piece of its answer is kept.
                var filled = FillPiece(work, remaining, pieceMask, pieceArea, cancellation);
                if (filled != null)
                {
                    var touched = pieceArea.Inflate(InpaintGeometry.DilationFor(pieceArea) + 6);
                    PutOver(result, filled, touched);
                    PutOver(work, filled, touched);
                }
            }
            for (int y = rect.Y; y < rect.Bottom; y++) Array.Clear(remaining, y * image.Width + rect.X, rect.Width);
            progress?.Report(++done / (double)total);
        }
        MatchDetail(result, image, mask, area);
        return result;
    }

    /// <summary>Sharpens the fill when the picture around it has clearly more fine detail than the (smooth) fill.</summary>
    private static void MatchDetail(PixelBuffer fill, PixelBuffer image, byte[] mask, PixelRect area)
    {
        int w = image.Width;
        var around = area.Inflate(80);
        double outside = InpaintDetail.DetailStrength(image, (x, y) => mask[y * w + x] == 0 && !area.Inflate(12).Contains(x, y), around);
        double inside = InpaintDetail.DetailStrength(fill, (x, y) => fill.Data[(y * w + x) * 4 + 3] == 255, area);
        InpaintDetail.Sharpen(fill, image, InpaintDetail.AmountFor(inside, outside));
    }

    /// <summary>
    /// One model run. <paramref name="hole"/> is everything the model must treat as empty; <paramref name="keep"/> (inside <paramref name="keepArea"/>)
    /// says which part of its answer is returned.
    /// </summary>
    private PixelBuffer? FillPiece(PixelBuffer image, byte[] hole, byte[] keep, PixelRect keepArea, CancellationToken cancellation)
    {
        int widen = InpaintGeometry.DilationFor(keepArea);       // the model is shown a bit more than was selected, so no edge of the removed object is left
        var bounds = new PixelRect(0, 0, image.Width, image.Height);
        var wideHole = InpaintGeometry.Dilate(hole, image.Width, image.Height, BoundsOf(hole, image.Width, image.Height), widen);
        var wideKeep = ReferenceEquals(hole, keep) ? wideHole : InpaintGeometry.Dilate(keep, image.Width, image.Height, keepArea, widen);
        var wideArea = keepArea.Inflate(widen).Intersect(bounds);
        var window = InpaintGeometry.PlanWindow(image.Width, image.Height, wideArea, _modelSize);
        var (input, holeInput) = InpaintGeometry.Prepare(image, wideHole, window, _modelSize);
        cancellation.ThrowIfCancellationRequested();

        float[] answer = Run(input, holeInput, cancellation);
        for (int i = 0; i < answer.Length; i++) answer[i] /= 255f;          // the model answers with 0..255
        cancellation.ThrowIfCancellationRequested();

        return InpaintGeometry.Compose(image.Width, image.Height, wideKeep, wideArea, answer, window, _modelSize);
    }

    /// <summary>The piece whose surroundings are known best (so the fill grows inward from the edge of the selection).</summary>
    private static int BestPiece(List<PixelRect> pieces, byte[] remaining, int width, int height)
    {
        int best = 0;
        double bestScore = -1;
        for (int i = 0; i < pieces.Count; i++)
        {
            var ring = pieces[i].Inflate(24).Intersect(new PixelRect(0, 0, width, height));
            long known = 0, count = 0;
            for (int y = ring.Y; y < ring.Bottom; y += 4)
                for (int x = ring.X; x < ring.Right; x += 4)
                {
                    if (pieces[i].Contains(x, y)) continue;
                    count++;
                    if (remaining[y * width + x] == 0) known++;
                }
            double score = count == 0 ? 0 : (double)known / count;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    private static bool AnySet(byte[] mask, int width, PixelRect rect)
    {
        for (int y = rect.Y; y < rect.Bottom; y++)
            if (mask.AsSpan(y * width + rect.X, rect.Width).IndexOfAnyExcept((byte)0) >= 0) return true;
        return false;
    }

    /// <summary>Paints <paramref name="piece"/> (straight alpha) over <paramref name="target"/> inside <paramref name="area"/>.</summary>
    private static void PutOver(PixelBuffer target, PixelBuffer piece, PixelRect area)
    {
        area = area.Intersect(target.Bounds);
        for (int y = area.Y; y < area.Bottom; y++)
            for (int x = area.X; x < area.Right; x++)
            {
                int i = (y * target.Width + x) * 4;
                int a = piece.Data[i + 3];
                if (a == 0) continue;
                if (a == 255) { target.Data[i] = piece.Data[i]; target.Data[i + 1] = piece.Data[i + 1]; target.Data[i + 2] = piece.Data[i + 2]; target.Data[i + 3] = 255; continue; }
                int ta = target.Data[i + 3], outA = a + ta * (255 - a) / 255;
                if (outA == 0) continue;
                for (int c = 0; c < 3; c++)
                    target.Data[i + c] = (byte)((piece.Data[i + c] * a + target.Data[i + c] * ta * (255 - a) / 255) / outA);
                target.Data[i + 3] = (byte)outA;
            }
    }

    private float[] Run(float[] image, float[] mask, CancellationToken cancellation)
    {
        lock (_lock)    // one run at a time: the model is big and parallel runs only waste memory
        {
            cancellation.ThrowIfCancellationRequested();
            var session = _session ??= Open();
            using var results = session.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("image", new DenseTensor<float>(image, new[] { 1, 3, _modelSize, _modelSize })),
                NamedOnnxValue.CreateFromTensor("mask", new DenseTensor<float>(mask, new[] { 1, 1, _modelSize, _modelSize })),
            });
            return results.First().AsEnumerable<float>().ToArray();
        }
    }

    private InferenceSession Open()
    {
        var session = OnnxSessionFactory.Create(_modelPath, out var device, allowGpu: false);
        DeviceName = device;
        return session;
    }

    /// <summary>The smallest rectangle around everything set in the mask.</summary>
    public static PixelRect BoundsOf(byte[] mask, int width, int height)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            var row = mask.AsSpan(y * width, width);
            int first = row.IndexOfAnyExcept((byte)0);
            if (first < 0) continue;
            int last = row.LastIndexOfAnyExcept((byte)0);
            if (first < minX) minX = first;
            if (last > maxX) maxX = last;
            if (y < minY) minY = y;
            maxY = y;
        }
        return maxX < 0 ? PixelRect.Empty : new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
