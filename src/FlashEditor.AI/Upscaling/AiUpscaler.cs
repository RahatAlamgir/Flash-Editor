using FlashEditor.AI.Models;
using FlashEditor.AI.Runtime;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FlashEditor.AI.Upscaling;

/// <summary>
/// AI upscaling / clean-up with Real-ESRGAN (4x model). The model works on fixed 128 x 128 tiles,
/// so the picture is processed tile by tile with overlaps that are blended smoothly (no seams).
/// Any output scale works: the 4x result of each tile is resized to the wanted scale (1 = same size,
/// which gives a cleaner, sharper picture). Transparency is enlarged separately (the model only does color).
/// </summary>
public sealed class AiUpscaler : IDisposable
{
    private const int ModelScale = 4;
    private const int Overlap = 16; // input pixels shared with neighboring tiles

    private readonly ModelInfo _model;
    private readonly string _modelPath;
    private readonly object _lock = new();
    private InferenceSession? _session;
    private string _device = "";

    public AiUpscaler(ModelInfo model, string modelPath)
    {
        _model = model;
        _modelPath = modelPath;
    }

    /// <summary>What the model runs on ("GPU (DirectML)" or "CPU"). Loads the model if needed.</summary>
    public string DeviceName
    {
        get
        {
            EnsureSession();
            return _device;
        }
    }

    private void EnsureSession()
    {
        lock (_lock) _session ??= OnnxSessionFactory.Create(_modelPath, out _device);
    }

    private int Tile => _model.InputSize;
    private int Step => Tile - 2 * Overlap;

    /// <summary>How many tiles a picture needs (for a time estimate: roughly 0.5-1.5 s each).</summary>
    public int TileCount(int width, int height) => Positions(width).Count * Positions(height).Count;

    /// <param name="scale">Output size factor (1 = same size, 2 = twice as large...).</param>
    public PixelBuffer Upscale(PixelBuffer source, double scale, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        int w = source.Width, h = source.Height;
        int outW = Math.Max(1, (int)Math.Round(w * scale)), outH = Math.Max(1, (int)Math.Round(h * scale));
        int tile = Tile;
        var color = new float[outW * outH * 3];
        var weight = new float[outW * outH];
        var xs = Positions(w);
        var ys = Positions(h);
        int done = 0, total = xs.Count * ys.Count;

        EnsureSession();

        foreach (int y0 in ys)
        {
            foreach (int x0 in xs)
            {
                cancellation.ThrowIfCancellationRequested();
                var output = RunTile(source, x0, y0);                    // tile*4 square, RGB 0..1, planar
                Blend(output, x0, y0, scale, w, h, outW, outH, color, weight);
                progress?.Report(++done / (double)total);
            }
        }

        // Transparency: smooth resize of the alpha channel (the model only handles color).
        var alpha = Resampler.Resize(source, outW, outH, ResampleMode.Lanczos);
        var result = new PixelBuffer(outW, outH);
        var d = result.Data;
        Parallel.For(0, outH, y =>
        {
            for (int x = 0; x < outW; x++)
            {
                int p = y * outW + x, i = p * 4;
                float wsum = Math.Max(1e-6f, weight[p]);
                d[i + 2] = ColorMath.ClampByte(color[p * 3] / wsum * 255);
                d[i + 1] = ColorMath.ClampByte(color[p * 3 + 1] / wsum * 255);
                d[i] = ColorMath.ClampByte(color[p * 3 + 2] / wsum * 255);
                d[i + 3] = alpha.Data[i + 3];
            }
        });
        return result;
    }

    /// <summary>Tile start positions along one side (the last tile is aligned to the end).</summary>
    private List<int> Positions(int length)
    {
        var list = new List<int>();
        if (length <= Tile) { list.Add(0); return list; }
        for (int p = 0; ; p += Step)
        {
            if (p + Tile >= length) { list.Add(length - Tile); break; }
            list.Add(p);
        }
        return list;
    }

    private float[] RunTile(PixelBuffer source, int x0, int y0)
    {
        int tile = Tile, plane = tile * tile;
        var input = new float[3 * plane];
        for (int y = 0; y < tile; y++)
        {
            int sy = Math.Clamp(y0 + y, 0, source.Height - 1);
            for (int x = 0; x < tile; x++)
            {
                int sx = Math.Clamp(x0 + x, 0, source.Width - 1); // small pictures: edges repeat
                int i = source.IndexOf(sx, sy), p = y * tile + x;
                input[p] = source.Data[i + 2] / 255f;
                input[plane + p] = source.Data[i + 1] / 255f;
                input[2 * plane + p] = source.Data[i] / 255f;
            }
        }
        lock (_lock)
        {
            var session = _session!;
            var tensor = new DenseTensor<float>(input, new[] { 1, 3, tile, tile });
            using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), tensor) });
            return results.First().AsEnumerable<float>().ToArray();
        }
    }

    /// <summary>
    /// Adds one tile's result (resized to the output scale) into the sums, with weights that fade out
    /// toward edges shared with other tiles, so overlapping tiles blend without visible seams.
    /// </summary>
    private void Blend(float[] output, int x0, int y0, double scale, int srcW, int srcH, int outW, int outH, float[] color, float[] weight)
    {
        int big = Tile * ModelScale, bigPlane = big * big;
        double fade = Overlap * scale;
        int left = (int)Math.Round(x0 * scale), top = (int)Math.Round(y0 * scale);
        int size = (int)Math.Round(Tile * scale);
        double ratio = big / (double)size;
        bool hasLeft = x0 > 0, hasTop = y0 > 0, hasRight = x0 + Tile < srcW, hasBottom = y0 + Tile < srcH;

        for (int y = 0; y < size; y++)
        {
            int oy = top + y;
            if (oy < 0 || oy >= outH) continue;
            double wy = EdgeWeight(y, size, fade, hasTop, hasBottom);
            for (int x = 0; x < size; x++)
            {
                int ox = left + x;
                if (ox < 0 || ox >= outW) continue;
                float wgt = (float)(wy * EdgeWeight(x, size, fade, hasLeft, hasRight));

                // Area-average the 4x result down to this output pixel (or sample it at 4x).
                double sx0 = x * ratio, sy0 = y * ratio, sx1 = sx0 + ratio, sy1 = sy0 + ratio;
                int ix0 = (int)sx0, iy0 = (int)sy0, ix1 = Math.Min(big, Math.Max(ix0 + 1, (int)Math.Ceiling(sx1))), iy1 = Math.Min(big, Math.Max(iy0 + 1, (int)Math.Ceiling(sy1)));
                double r = 0, g = 0, b = 0;
                int n = 0;
                for (int yy = iy0; yy < iy1; yy++)
                    for (int xx = ix0; xx < ix1; xx++)
                    {
                        int q = yy * big + xx;
                        r += output[q]; g += output[bigPlane + q]; b += output[2 * bigPlane + q];
                        n++;
                    }
                int p = oy * outW + ox;
                color[p * 3] += (float)(r / n) * wgt;
                color[p * 3 + 1] += (float)(g / n) * wgt;
                color[p * 3 + 2] += (float)(b / n) * wgt;
                weight[p] += wgt;
            }
        }
    }

    /// <summary>1 in the middle of a tile, fading to almost 0 at edges that overlap a neighbor.</summary>
    private static double EdgeWeight(int i, int size, double fade, bool fadeStart, bool fadeEnd)
    {
        double w = 1;
        if (fadeStart) w = Math.Min(w, (i + 0.5) / fade);
        if (fadeEnd) w = Math.Min(w, (size - i - 0.5) / fade);
        return Math.Clamp(w, 0.001, 1);
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
