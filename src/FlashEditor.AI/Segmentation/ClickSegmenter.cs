using FlashEditor.AI.Models;
using FlashEditor.AI.Runtime;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FlashEditor.AI.Segmentation;

/// <summary>One thing the user told the AI: a click on the object ("include"), a click beside it ("exclude"), or a box around it.</summary>
public readonly record struct ClickPrompt(IReadOnlyList<(double X, double Y, bool Include)> Points, (double X1, double Y1, double X2, double Y2)? Box = null);

/// <summary>
/// Click to select (MobileSAM): the picture is studied once (the slow part, a second or two), then every click or box gives an outline of
/// the object under it almost at once. The study result is kept, so more clicks on the same picture are fast.
/// </summary>
public sealed class ClickSegmenter : IDisposable
{
    private const int EncoderSize = 1024;
    private readonly string _encoderPath, _decoderPath;
    private readonly object _lock = new();
    private InferenceSession? _encoder, _decoder;

    private (int W, int H, ulong Hash) _cacheKey;
    private float[]? _embeddings;

    public ClickSegmenter(string encoderPath, string decoderPath)
    {
        _encoderPath = encoderPath;
        _decoderPath = decoderPath;
    }

    /// <summary>What the model runs on (always the processor; empty until first used).</summary>
    public string DeviceName { get; private set; } = "";

    /// <summary>The selected area as 0 / 255 per pixel at the picture's size (empty when the AI found nothing).</summary>
    public byte[] Predict(PixelBuffer image, ClickPrompt prompt, CancellationToken cancellation = default)
    {
        lock (_lock)
        {
            var embeddings = Embed(image, cancellation);
            cancellation.ThrowIfCancellationRequested();

            var (coords, labels) = BuildPrompt(prompt, image.Width, image.Height);
            int n = labels.Length;
            var decoder = _decoder ??= new InferenceSession(_decoderPath, new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image_embeddings", new DenseTensor<float>(embeddings, new[] { 1, 256, 64, 64 })),
                NamedOnnxValue.CreateFromTensor("point_coords", new DenseTensor<float>(coords, new[] { 1, n, 2 })),
                NamedOnnxValue.CreateFromTensor("point_labels", new DenseTensor<float>(labels, new[] { 1, n })),
                NamedOnnxValue.CreateFromTensor("mask_input", new DenseTensor<float>(new float[256 * 256], new[] { 1, 1, 256, 256 })),
                NamedOnnxValue.CreateFromTensor("has_mask_input", new DenseTensor<float>(new float[] { 0 }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("orig_im_size", new DenseTensor<float>(new float[] { image.Height, image.Width }, new[] { 2 })),
            };
            using var results = decoder.Run(inputs);
            var logits = results.First(r => r.Name == "masks").AsEnumerable<float>().ToArray();
            var mask = new byte[image.Width * image.Height];
            if (logits.Length < mask.Length) return mask;
            for (int i = 0; i < mask.Length; i++) mask[i] = logits[i] > 0 ? (byte)255 : (byte)0;
            return mask;
        }
    }

    /// <summary>
    /// The decoder's point list (in the 1024 pixel space the model works in) and labels: 1 = on the object, 0 = not the object,
    /// 2 / 3 = top-left / bottom-right of a box. Without a box the model wants one extra "padding" point with label -1.
    /// </summary>
    public static (float[] Coords, float[] Labels) BuildPrompt(ClickPrompt prompt, int width, int height)
    {
        double scale = EncoderSize / (double)Math.Max(width, height);
        var coords = new List<float>();
        var labels = new List<float>();
        void Add(double x, double y, float label)
        {
            coords.Add((float)(x * scale));
            coords.Add((float)(y * scale));
            labels.Add(label);
        }
        foreach (var (x, y, include) in prompt.Points) Add(x, y, include ? 1 : 0);
        if (prompt.Box is { } b)
        {
            Add(Math.Min(b.X1, b.X2), Math.Min(b.Y1, b.Y2), 2);
            Add(Math.Max(b.X1, b.X2), Math.Max(b.Y1, b.Y2), 3);
        }
        else Add(0, 0, -1);
        return (coords.ToArray(), labels.ToArray());
    }

    // Studies the picture (or returns the result kept from the last time for the same picture).
    private float[] Embed(PixelBuffer image, CancellationToken cancellation)
    {
        var key = (image.Width, image.Height, ContentHash(image));
        if (_embeddings != null && _cacheKey == key) return _embeddings;

        double scale = EncoderSize / (double)Math.Max(image.Width, image.Height);
        int w = Math.Max(1, (int)Math.Round(image.Width * scale)), h = Math.Max(1, (int)Math.Round(image.Height * scale));
        var small = w == image.Width && h == image.Height ? image : Resampler.Resize(image, w, h, ResampleMode.Smooth);
        var input = new float[h * w * 3];                    // height x width x 3, RGB, 0..255 (the model normalizes and pads itself)
        var d = small.Data;
        for (int p = 0; p < w * h; p++)
        {
            input[p * 3] = d[p * 4 + 2];
            input[p * 3 + 1] = d[p * 4 + 1];
            input[p * 3 + 2] = d[p * 4];
        }
        cancellation.ThrowIfCancellationRequested();

        var tensor = new DenseTensor<float>(input, new[] { h, w, 3 });
        float[] result = RunEncoder(_encoder ??= CreateEncoder(), tensor);
        _cacheKey = key;
        _embeddings = result;
        return result;
    }

    private static float[] RunEncoder(InferenceSession session, DenseTensor<float> tensor)
    {
        using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor("input_image", tensor) });
        return results.First().AsEnumerable<float>().ToArray();
    }

    private InferenceSession CreateEncoder()
    {
        var session = OnnxSessionFactory.Create(_encoderPath, out var device, allowGpu: false);   // on the graphics card this model gives wrong outlines
        DeviceName = device;
        return session;
    }

    private static ulong ContentHash(PixelBuffer image)
    {
        var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ulong>(image.Data);
        ulong hash = 14695981039346656037UL;
        foreach (ulong w in words) hash = (hash ^ w) * 1099511628211UL;
        for (int i = words.Length * 8; i < image.Data.Length; i++) hash = (hash ^ image.Data[i]) * 1099511628211UL;
        return hash;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _encoder?.Dispose();
            _decoder?.Dispose();
            _encoder = _decoder = null;
            _embeddings = null;
        }
    }
}
