using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// An editable object living on its own layer (Canva-style): a shape, a text or a placed image.
/// The layer remembers the object and redraws its pixels whenever the object changes, so color,
/// border, size, rotation or text can be changed at any time. Objects are immutable: every change
/// makes a new object (which makes undo simple).
/// </summary>
public abstract record LayerObject(ObjectBox Box, bool FlipX = false, bool FlipY = false)
{
    /// <summary>The drop shadow behind the object, or null for none (any kind of object can have one).</summary>
    public ShadowStyle? Shadow { get; init; }

    /// <summary>A fading color for the fill of a shape or the color of a text, or null for a flat color. (Pictures and groups ignore it.)</summary>
    public GradientStyle? Gradient { get; init; }

    /// <summary>The 3D look (a solid side and a bevel), or null for a flat object (any kind of object can have one).</summary>
    public ThreeDStyle? ThreeD { get; init; }

    /// <summary>
    /// Reshape for objects without corner points of their own (pictures, text, groups, curved shapes): where the four corners
    /// of the object (top-left, top-right, bottom-right, bottom-left) are, in the box (0,0 = top-left of the box, 1,1 = bottom-right).
    /// They may lie outside the box. Null = the normal, undistorted object.
    /// </summary>
    public IReadOnlyList<PointD>? Warp { get; init; }

    /// <summary>Short name for menus and the history ("Shape", "Text", "Image").</summary>
    public abstract string KindName { get; }

    /// <summary>Default layer name for a new layer holding this object.</summary>
    public virtual string SuggestedLayerName => KindName;

    /// <summary>
    /// True when corner handles should keep the width/height ratio by default (images and text).
    /// Shapes stretch freely; Shift switches the behavior either way.
    /// </summary>
    public virtual bool KeepsAspectByDefault => true;

    /// <summary>True when the object can only be resized along its width (a line).</summary>
    public virtual bool IsLineLike => false;

    // ------------------------------------------------------------------ reshape (dragging corner points)

    /// <summary>The unit square's corners: where the four corner points are when nothing was dragged.</summary>
    public static readonly IReadOnlyList<PointD> DefaultCorners = new[] { new PointD(0, 0), new PointD(1, 0), new PointD(1, 1), new PointD(0, 1) };

    /// <summary>True when the corner points can be dragged (every object except a line).</summary>
    public virtual bool CanReshape => !IsLineLike;

    /// <summary>True once the corners were dragged (the object no longer has its normal form).</summary>
    public virtual bool HasCustomOutline => Warp != null;

    /// <summary>The points the user drags: the object's corners (in the box, may lie outside it). Null when it cannot be reshaped.</summary>
    public virtual IReadOnlyList<PointD>? ReshapePoints => CanReshape ? Warp ?? DefaultCorners : null;

    /// <summary>The object with its reshape points set to <paramref name="points"/> (the same number as <see cref="ReshapePoints"/>).</summary>
    public virtual LayerObject WithReshapePoints(IReadOnlyList<PointD> points) => this with { Warp = points };

    /// <summary>The object with its normal form back.</summary>
    public virtual LayerObject WithoutReshape() => this with { Warp = null };

    /// <summary>A point of the canvas as a position in the object (0,0 = top-left of its box, 1,1 = bottom-right), however it is turned or mirrored.</summary>
    public PointD ToUnit(PointD canvasPoint)
    {
        var local = Box.ToLocal(canvasPoint);
        double x = FlipX ? -local.X : local.X, y = FlipY ? -local.Y : local.Y;
        return new PointD(x / Math.Max(1e-9, Box.Width) + 0.5, y / Math.Max(1e-9, Box.Height) + 0.5);
    }

    /// <summary>Where a position in the object (0..1 in both directions, or beyond) is on the canvas.</summary>
    public PointD FromUnit(PointD unit)
    {
        double x = (unit.X - 0.5) * Box.Width * (FlipX ? -1 : 1);
        double y = (unit.Y - 0.5) * Box.Height * (FlipY ? -1 : 1);
        return Box.ToCanvas(x, y);
    }

    /// <summary>
    /// Rectangle (canvas pixels, grown by <paramref name="margin"/>) around everything the object covers: its box and, once
    /// the corners were dragged outward, those corner points too.
    /// </summary>
    public PixelRect VisualBounds(double margin = 0)
    {
        var points = Box.Corners().ToList();
        if (ReshapeOutline is { } outline) points.AddRange(outline.Select(FromUnit));
        double l = points.Min(p => p.X) - margin, t = points.Min(p => p.Y) - margin;
        double r = points.Max(p => p.X) + margin, b = points.Max(p => p.Y) + margin;
        return PixelRect.FromBounds(l, t, r, b);
    }

    /// <summary>The custom corner points in effect (null when the object has its normal form).</summary>
    protected virtual IReadOnlyList<PointD>? ReshapeOutline => Warp;
}
