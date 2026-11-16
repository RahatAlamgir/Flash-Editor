using FlashEditor.Core.Common;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Documents;

/// <summary>
/// One layer of a document: its pixels plus name, opacity and visibility.
/// Code that edits <see cref="Pixels"/> directly must call <see cref="NotifyPixelsChanged(PixelRect)"/>
/// afterwards so the screen refreshes.
/// <para>
/// <see cref="Pixels"/> is always exactly the canvas size, so tools and effects never have to think about
/// offsets. Pixels moved past the canvas edge are not lost: they are kept in a hidden, larger buffer
/// (see <see cref="LayerState"/>) and come back when the layer is moved back or the canvas is enlarged.
/// </para>
/// </summary>
public sealed class Layer : ObservableObject
{
    private string _name;
    private double _opacity = 1.0;
    private bool _isVisible = true;
    private bool _isLocked;

    // Larger copy of the layer including parts outside the canvas (null when nothing is hidden).
    private PixelBuffer? _extended;
    private int _extendedX, _extendedY;

    public Layer(string name, PixelBuffer pixels)
    {
        _name = name;
        Pixels = pixels;
    }

    public Layer(string name, int width, int height) : this(name, new PixelBuffer(width, height)) { }

    /// <summary>Raised after a region of pixels was edited in place.</summary>
    public event EventHandler<PixelRect>? PixelsChanged;

    /// <summary>Raised after <see cref="Pixels"/> was swapped for a new buffer (possibly a new size).</summary>
    public event EventHandler? PixelsReplaced;

    /// <summary>The part of the layer that lies on the canvas (always canvas-sized).</summary>
    public PixelBuffer Pixels { get; private set; }

    public int Width => Pixels.Width;
    public int Height => Pixels.Height;

    /// <summary>Goes up by one every time the pixels change (lets tools detect "nothing changed since").</summary>
    public long Version { get; private set; }

    /// <summary>
    /// The editable object on this layer (shape, text, placed image), or null for a normal pixel layer.
    /// The pixels of an object layer are always a rendering of the object.
    /// </summary>
    public LayerObject? Object { get; private set; }

    public bool IsObjectLayer => Object != null;

    /// <summary>True if some of this layer's pixels lie outside the canvas.</summary>
    public bool HasHiddenPixels => _extended != null;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value ?? string.Empty);
    }

    /// <summary>0 (invisible) .. 1 (fully opaque).</summary>
    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, Math.Clamp(value, 0.0, 1.0));
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetField(ref _isVisible, value);
    }

    /// <summary>
    /// A locked layer cannot be moved, resized, painted, edited or deleted (it can still be selected,
    /// copied, hidden and unlocked).
    /// </summary>
    public bool IsLocked
    {
        get => _isLocked;
        set => SetField(ref _isLocked, value);
    }

    /// <summary>
    /// The whole layer including hidden parts, and where its top-left corner is on the canvas.
    /// The buffer is live: do not modify it.
    /// </summary>
    public (PixelBuffer Content, int X, int Y) GetFullContent()
        => _extended != null ? (_extended, _extendedX, _extendedY) : (Pixels, 0, 0);

    /// <summary>Area covered by the layer's non-transparent pixels, in canvas coordinates (may go outside the canvas).</summary>
    public PixelRect VisibleBounds()
    {
        var (content, x, y) = GetFullContent();
        return content.VisibleBounds().Offset(x, y);
    }

    public void NotifyPixelsChanged(PixelRect region)
    {
        region = region.Intersect(Pixels.Bounds);
        if (region.IsEmpty) return;

        // Keep the hidden full copy in sync with edits made on the canvas.
        _extended?.CopyFrom(Pixels, region, region.X - _extendedX, region.Y - _extendedY);
        Version++;
        PixelsChanged?.Invoke(this, region);
    }

    public void NotifyPixelsChanged() => NotifyPixelsChanged(Pixels.Bounds);

    /// <summary>Replaces the pixels with a new buffer. Any hidden off-canvas pixels are dropped.</summary>
    public void ReplacePixels(PixelBuffer pixels) => RestoreState(LayerState.Simple(pixels));

    /// <summary>The current buffers (not copies). Used by undo steps that swap whole layers.</summary>
    public LayerState SaveState() => new(Pixels, _extended, _extendedX, _extendedY, Object);

    /// <summary>
    /// Makes this an object layer showing <paramref name="obj"/> (redraws the pixels), or, with null,
    /// turns it into a normal pixel layer that keeps its current pixels ("convert to pixels").
    /// </summary>
    public void SetObject(LayerObject? obj)
    {
        if (obj == null)
        {
            if (Object == null) return;
            RestoreState(SaveState() with { Object = null });
            return;
        }
        RestoreState(RenderState(obj, Width, Height));
    }

    /// <summary>The layer state for <paramref name="obj"/> drawn on a canvas of the given size.</summary>
    public static LayerState RenderState(LayerObject obj, int canvasWidth, int canvasHeight)
        => new(ObjectRendering.Renderer.Render(obj, canvasWidth, canvasHeight), null, 0, 0, obj);

    /// <summary>Makes <paramref name="state"/> the layer's content. The buffers are used as they are (not copied).</summary>
    public void RestoreState(LayerState state)
    {
        Pixels = state.Pixels;
        _extended = state.Extended;
        _extendedX = state.ExtendedX;
        _extendedY = state.ExtendedY;
        bool objectChanged = !ReferenceEquals(Object, state.Object);
        Object = state.Object;
        Version++;
        OnPropertyChanged(nameof(Pixels));
        OnPropertyChanged(nameof(HasHiddenPixels));
        if (objectChanged)
        {
            OnPropertyChanged(nameof(Object));
            OnPropertyChanged(nameof(IsObjectLayer));
        }
        PixelsReplaced?.Invoke(this, EventArgs.Empty);
    }

    public Layer Duplicate(string name)
    {
        var copy = new Layer(name, Pixels.Clone()) { Opacity = Opacity, IsVisible = IsVisible };
        if (_extended != null || Object != null) copy.RestoreState(SaveState().DeepCopy());
        return copy;
    }

    public override string ToString() => Name;
}
