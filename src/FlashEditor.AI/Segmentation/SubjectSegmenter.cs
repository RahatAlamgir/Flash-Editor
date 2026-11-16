using FlashEditor.AI.Models;
using FlashEditor.AI.Runtime;
using FlashEditor.Core.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FlashEditor.AI.Segmentation;

/// <summary>
/// Runs the background-removal model: given a picture, returns how much each pixel belongs to the
/// main subject (0 = background, 1 = subject). The model is loaded on first use and kept in memory.
/// The last result is cached, so moving a slider in the dialog does not run the model again.
/// </summary>
public sealed class SubjectSegmenter : IDisposable
{
    private readonly ModelInfo _model;
    private readonly string _modelPath;
    private readonly object _lock = new();
    private InferenceSession? _session;

    private (int W, int H, ulong Hash) _cacheKey;
    private float[]? _cachedMask;

    public SubjectSegmenter(ModelInfo model, string modelPath)
    {
        _model = model;
        _modelPath = modelPath;
    }

    /// <summary>The subject mask at the image's own size. Can take a few seconds the first time.</summary>
    public float[] PredictMask(PixelBuffer image, CancellationToken cancellation = default)
    {
        var key = (image.Width, image.Height, ContentHash(image));
        lock (_lock)
        {
            if (_cachedMask != null && _cacheKey == key) return (float[])_cachedMask.Clone();
        }

        int size = _model.InputSize;
        var input = MaskOps.ToInputTensor(image, size, _model.Mean, _model.Std);
        cancellation.ThrowIfCancellationRequested();

        float[] raw;
        lock (_lock) // one inference at a time (the session is large; parallel runs only waste memory)
        {
            cancellation.ThrowIfCancellationRequested();
            var session = _session ??= CreateSession();
            var tensor = new DenseTensor<float>(input, new[] { 1, 3, size, size });
            string inputName = session.InputMetadata.Keys.First();
            using var results = session.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) });
            raw = results.First().AsEnumerable<float>().ToArray();
        }
        cancellation.ThrowIfCancellationRequested();

        int side = (int)Math.Round(Math.Sqrt(raw.Length)); // output is side x side (usually = input size)
        var mask = MaskOps.Resize(MaskOps.Normalize(raw), side, side, image.Width, image.Height);

        lock (_lock)
        {
            _cacheKey = key;
            _cachedMask = mask;
        }
        return (float[])mask.Clone();
    }

    /// <summary>What the model runs on ("GPU (DirectML)" or "CPU"; empty until first used).</summary>
    public string DeviceName { get; private set; } = "";

    private InferenceSession CreateSession()
    {
        var session = OnnxSessionFactory.Create(_modelPath, out var device);
        DeviceName = device;
        return session;
    }

    /// <summary>Fast fingerprint of the pixels (to know whether the cached mask still fits).</summary>
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
            _session?.Dispose();
            _session = null;
        }
    }
}
