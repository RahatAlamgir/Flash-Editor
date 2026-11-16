using System.Collections.ObjectModel;
using FlashEditor.Core.Common;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Documents;

/// <summary>
/// An open image: canvas size, a stack of layers (index 0 = bottom), the active layer,
/// the current selection and the undo/redo history.
/// </summary>
public sealed class Document : ObservableObject
{
    private int _width;
    private int _height;
    private Layer? _activeLayer;
    private Selection? _selection;
    private int _layerCounter;

    public Document(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Canvas size must be positive.");
        _width = width;
        _height = height;
        History = new HistoryManager();
    }

    public int Width { get => _width; private set => SetField(ref _width, value); }
    public int Height { get => _height; private set => SetField(ref _height, value); }
    public PixelRect Bounds => new(0, 0, Width, Height);

    /// <summary>Layers from bottom (index 0) to top.</summary>
    public ObservableCollection<Layer> Layers { get; } = new();

    public HistoryManager History { get; }

    /// <summary>Raised after the canvas changed size (crop, resize, rotate).</summary>
    public event EventHandler? CanvasSizeChanged;

    public Layer? ActiveLayer
    {
        get => _activeLayer;
        set
        {
            if (value != null && !Layers.Contains(value))
                throw new InvalidOperationException("The active layer must belong to the document.");
            SetField(ref _activeLayer, value);
        }
    }

    public int ActiveLayerIndex => _activeLayer == null ? -1 : Layers.IndexOf(_activeLayer);

    /// <summary>The current selection, or null when nothing is selected (= everything is editable).</summary>
    public Selection? Selection
    {
        get => _selection;
        set => SetField(ref _selection, value);
    }

    /// <summary>Creates a document with one layer filled with <paramref name="background"/>.</summary>
    public static Document CreateBlank(int width, int height, ColorRgba background)
    {
        var doc = new Document(width, height);
        var pixels = new PixelBuffer(width, height);
        if (background.A > 0) pixels.Fill(background);
        doc.AddInitialLayer(new Layer("Background", pixels));
        return doc;
    }

    /// <summary>Creates a document from a decoded image (one layer).</summary>
    public static Document FromImage(PixelBuffer image, string layerName = "Background")
    {
        var doc = new Document(image.Width, image.Height);
        doc.AddInitialLayer(new Layer(layerName, image));
        return doc;
    }

    /// <summary>Suggests a fresh name like "Layer 3".</summary>
    public string NextLayerName(string prefix = "Layer")
    {
        _layerCounter++;
        return $"{prefix} {_layerCounter}";
    }

    /// <summary>Replaces the layer stack (used by layer commands and undo/redo).</summary>
    public void SetLayers(IReadOnlyList<Layer> layers, Layer? active)
    {
        // Only touch the collection when something actually changed to avoid UI churn.
        if (!Layers.SequenceEqual(layers))
        {
            ActiveLayer = null;
            Layers.Clear();
            foreach (var layer in layers) Layers.Add(layer);
        }
        ActiveLayer = active != null && Layers.Contains(active) ? active : Layers.LastOrDefault();
    }

    /// <summary>Changes the canvas size. Callers are responsible for giving each layer matching pixels.</summary>
    public void SetCanvasSize(int width, int height)
    {
        if (width == Width && height == Height) return;
        Width = width;
        Height = height;
        Selection = null;
        CanvasSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddInitialLayer(Layer layer)
    {
        Layers.Add(layer);
        ActiveLayer = layer;
    }
}
