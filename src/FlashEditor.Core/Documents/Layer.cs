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
    private BlendMode _blendMode;
    private AdjustmentStyle? _adjustment;
    private LayerMask? _mask;
    private bool _maskEnabled = true;

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

    /// <summary>Raised after a part of the mask was painted (the area), so the screen can redraw the layer there.</summary>
    public event EventHandler<PixelRect>? MaskChanged;

    /// <summary>Raised after the mask was added, removed, replaced or switched on / off.</summary>
    public event EventHandler? MaskReplaced;

    /// <summary>
    /// The layer mask (canvas sized), or null. Where it is dark the layer is hidden; the layer's pixels are never changed by it.
    /// </summary>
    public LayerMask? Mask => _mask;

    public bool HasMask => _mask != null;

    /// <summary>False = the mask is kept but ignored (to see the layer without it).</summary>
    public bool MaskEnabled
    {
        get => _maskEnabled;
        set
        {
            if (!SetField(ref _maskEnabled, value)) return;
            Version++;
            MaskReplaced?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The mask that is in use when the layer is drawn: the mask, if there is one and it is switched on.</summary>
    public LayerMask? EffectiveMask => _maskEnabled ? _mask : null;

    /// <summary>Adds, replaces or (with null) removes the mask. The mask must have the canvas size.</summary>
    public void SetMask(LayerMask? mask)
    {
        if (mask != null && (mask.Width != Pixels.Width || mask.Height != Pixels.Height))
            throw new ArgumentException("The mask must have the size of the canvas.", nameof(mask));
        _mask = mask;
        Version++;
        OnPropertyChanged(nameof(Mask));
        OnPropertyChanged(nameof(HasMask));
        MaskReplaced?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Tells the screen that part of the mask was painted.</summary>
    public void NotifyMaskChanged(PixelRect region)
    {
        region = region.Intersect(Pixels.Bounds);
        if (region.IsEmpty) return;
        Version++;
        MaskChanged?.Invoke(this, region);
    }

    /// <summary>The pixels with the mask applied (the layer as it looks). The same buffer when there is no mask in use.</summary>
    public PixelBuffer MaskedPixels() => EffectiveMask is { } mask ? LayerMask.Bake(Pixels, mask) : Pixels;

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

    /// <summary>
    /// The layer's pixels from before its background was removed (canvas-sized), so the Refine brush
    /// can paint parts back. Null when there is nothing to restore from.
    /// </summary>
    public PixelBuffer? RestoreSource { get; set; }

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

    /// <summary>
    /// Set for an adjustment layer: it has no picture of its own, it changes the colors of all layers below it (brightness, contrast...).
    /// </summary>
    public AdjustmentStyle? Adjustment
    {
        get => _adjustment;
        set { if (SetField(ref _adjustment, value)) OnPropertyChanged(nameof(IsAdjustment)); }
    }

    public bool IsAdjustment => _adjustment != null;

    /// <summary>How this layer mixes with the layers below it.</summary>
    public BlendMode BlendMode
    {
        get => _blendMode;
        set => SetField(ref _blendMode, value);
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
    public LayerState SaveState()
    {
        _ownsPixels = false; // someone may keep these buffers (undo), so they must not be changed in place any more
        return new(Pixels, _extended, _extendedX, _extendedY, Object, _mask, IncludesMask: true);
    }

    // True while Pixels is a buffer made by SetObject that nobody else holds. Only then can the next SetObject
    // redraw just the changed part of it (dragging an object redraws it many times per second).
    private bool _ownsPixels;

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

        if (Object != null && _ownsPixels && _extended == null)
        {
            RedrawObjectInPlace(obj);
            return;
        }
        RestoreState(RenderState(obj, Width, Height));
        _ownsPixels = true; // RenderState made a brand-new buffer that only this layer holds
    }

    /// <summary>
    /// Replaces the object and redraws only the area the old and the new drawing cover. Same result as drawing a
    /// whole new canvas, but the cost depends on the object's size instead of the picture's size.
    /// </summary>
    private void RedrawObjectInPlace(LayerObject obj)
    {
        var renderer = ObjectRendering.Renderer;
        if (TryShiftObject(renderer, obj)) return;

        var oldArea = renderer.PaintedArea(Object!, Width, Height);
        var newArea = renderer.PaintedArea(obj, Width, Height);

        Pixels.ClearRegion(oldArea);
        renderer.RenderInto(Pixels, obj);

        Object = obj;
        NotifyPixelsChanged(oldArea.Union(newArea)); // raises PixelsChanged for just that area
        OnPropertyChanged(nameof(Object));
    }

    /// <summary>
    /// When the object only moved by whole pixels (same size, rotation, colors, text...), its drawing is identical
    /// except for the position, so the old pixels are copied to the new place instead of drawing it again. This is
    /// what happens most while dragging. Only when both the old and the new drawing lie completely on the canvas
    /// (nothing was cut off at the edge). Returns false when the object has to be drawn normally.
    /// </summary>
    private bool TryShiftObject(IObjectRenderer renderer, LayerObject obj)
    {
        var old = Object!;
        if (obj.Box.Width != old.Box.Width || obj.Box.Height != old.Box.Height || obj.Box.Rotation != old.Box.Rotation) return false;
        if (!Equals(obj with { Box = old.Box }, old)) return false; // something else changed too (color, text, shadow...)

        double dx = obj.Box.X - old.Box.X, dy = obj.Box.Y - old.Box.Y;
        if (dx != Math.Round(dx) || dy != Math.Round(dy) || Math.Abs(dx) > int.MaxValue / 2 || Math.Abs(dy) > int.MaxValue / 2) return false;

        var from = renderer.DrawingBounds(old);
        var to = renderer.DrawingBounds(obj);
        var canvas = Pixels.Bounds;
        if (from.IsEmpty || to.IsEmpty) return false;
        if (to.X - from.X != (int)dx || to.Y - from.Y != (int)dy) return false;

        if (from.Intersect(canvas) == from && to.Intersect(canvas) == to)
        {
            var content = Pixels.Crop(from);   // a copy: the old and the new place may overlap
            Pixels.ClearRegion(from);
            Pixels.CopyFrom(content, to.X, to.Y);
        }
        else
        {
            // Part of the drawing is (or was) beyond the canvas edge, so the canvas pixels are not the whole picture of the object.
            // Keep one complete drawing of it and place that: moving an object over the edge costs no more than moving it inside.
            var sprite = SpriteFor(renderer, old, from);
            Pixels.ClearRegion(from);
            Pixels.CopyFrom(sprite, to.X, to.Y);
        }

        Object = obj;
        NotifyPixelsChanged(from.Union(to));
        OnPropertyChanged(nameof(Object));
        return true;
    }

    // A complete drawing of an object that is partly off the canvas (see TryShiftObject), valid for any object that only differs by position.
    private PixelBuffer? _sprite;
    private LayerObject? _spriteObject;

    private PixelBuffer SpriteFor(IObjectRenderer renderer, LayerObject obj, PixelRect bounds)
    {
        if (_sprite != null && _spriteObject != null && _sprite.Width == bounds.Width && _sprite.Height == bounds.Height
            && Equals(obj with { Box = _spriteObject.Box }, _spriteObject)
            && obj.Box.Width == _spriteObject.Box.Width && obj.Box.Height == _spriteObject.Box.Height && obj.Box.Rotation == _spriteObject.Box.Rotation)
        {
            return _sprite;
        }

        // Draw the object alone on a buffer exactly as big as its drawing, with that drawing's top-left corner at (0, 0).
        var moved = obj with { Box = obj.Box.Offset(-bounds.X, -bounds.Y) };
        _sprite = renderer.Render(moved, bounds.Width, bounds.Height);
        _spriteObject = obj;
        return _sprite;
    }

    /// <summary>The layer state for <paramref name="obj"/> drawn on a canvas of the given size.</summary>
    public static LayerState RenderState(LayerObject obj, int canvasWidth, int canvasHeight)
        => new(ObjectRendering.Renderer.Render(obj, canvasWidth, canvasHeight), null, 0, 0, obj);

    /// <summary>Makes <paramref name="state"/> the layer's content. The buffers are used as they are (not copied).</summary>
    public void RestoreState(LayerState state)
    {
        _ownsPixels = false; // these buffers also belong to an undo step (or the caller)
        _sprite = null; _spriteObject = null; // a different layer content: drop the cached drawing
        Pixels = state.Pixels;
        _extended = state.Extended;
        _extendedX = state.ExtendedX;
        _extendedY = state.ExtendedY;
        bool objectChanged = !ReferenceEquals(Object, state.Object);
        Object = state.Object;
        // A state made by SaveState brings its mask back; other states (moving pixels, cropping...) leave the mask where it is.
        bool maskChanged = false;
        if (state.IncludesMask && !ReferenceEquals(_mask, state.Mask)) { _mask = state.Mask; maskChanged = true; }
        if (_mask != null && (_mask.Width != Pixels.Width || _mask.Height != Pixels.Height)) { _mask = null; maskChanged = true; }
        Version++;
        OnPropertyChanged(nameof(Pixels));
        OnPropertyChanged(nameof(HasHiddenPixels));
        if (objectChanged)
        {
            OnPropertyChanged(nameof(Object));
            OnPropertyChanged(nameof(IsObjectLayer));
        }
        if (maskChanged)
        {
            OnPropertyChanged(nameof(Mask));
            OnPropertyChanged(nameof(HasMask));
        }
        PixelsReplaced?.Invoke(this, EventArgs.Empty);
        if (maskChanged) MaskReplaced?.Invoke(this, EventArgs.Empty);
    }

    public Layer Duplicate(string name)
    {
        var copy = new Layer(name, Pixels.Clone()) { Opacity = Opacity, IsVisible = IsVisible, BlendMode = BlendMode, Adjustment = Adjustment, RestoreSource = RestoreSource };
        if (_extended != null || Object != null) copy.RestoreState(SaveState().DeepCopy() with { Mask = null, IncludesMask = false });
        if (_mask != null) { copy.SetMask(_mask.Clone()); copy._maskEnabled = _maskEnabled; }
        return copy;
    }

    public override string ToString() => Name;
}
