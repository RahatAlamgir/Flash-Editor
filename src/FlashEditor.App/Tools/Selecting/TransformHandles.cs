using System.Windows.Input;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Tools.Selecting;

public enum HandleKind { None, Move, NW, N, NE, E, SE, S, SW, W, Rotate }

/// <summary>
/// The box with handles around the selected layer (Canva-style): where the handles are on screen,
/// which one the mouse is over, and the matching cursor. Shared by the canvas (drawing) and the
/// Move tool (dragging), so both always agree.
/// </summary>
public static class TransformHandles
{
    /// <summary>Handle size and rotate-handle distance, in screen pixels.</summary>
    public const double HandleSize = 10;
    public const double RotateDistance = 28;

    /// <summary>The frame of a layer: its object's box, or the area of its visible pixels. Null when empty.</summary>
    public static ObjectBox? FrameOf(Layer? layer)
    {
        if (layer == null || !layer.IsVisible) return null;
        if (layer.Object is { } obj) return obj.Box;
        var bounds = layer.VisibleBounds();
        return bounds.IsEmpty ? null : new ObjectBox(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>The frame of the selected part of a group when there is one, otherwise <see cref="FrameOf(Layer?)"/>.</summary>
    public static ObjectBox? FrameOf(Layer? layer, int? part)
    {
        if (part == null) return FrameOf(layer);
        return layer is { IsVisible: true } ? new EditTarget(layer, part).Object?.Box : null;
    }

    /// <summary>Direction of each resize handle (-1, 0 or 1 per axis) in box space.</summary>
    public static (int Sx, int Sy) Direction(HandleKind kind) => kind switch
    {
        HandleKind.NW => (-1, -1), HandleKind.N => (0, -1), HandleKind.NE => (1, -1), HandleKind.E => (1, 0),
        HandleKind.SE => (1, 1), HandleKind.S => (0, 1), HandleKind.SW => (-1, 1), HandleKind.W => (-1, 0),
        _ => (0, 0),
    };

    /// <summary>Visible handles for a box. Lines only get end handles; text only corners.</summary>
    public static IEnumerable<HandleKind> HandlesFor(LayerObject? obj)
    {
        if (obj?.IsLineLike == true) return new[] { HandleKind.W, HandleKind.E };
        if (obj is TextObject) return new[] { HandleKind.NW, HandleKind.NE, HandleKind.SE, HandleKind.SW };
        return new[] { HandleKind.NW, HandleKind.N, HandleKind.NE, HandleKind.E, HandleKind.SE, HandleKind.S, HandleKind.SW, HandleKind.W };
    }

    public static PointD Position(ObjectBox box, HandleKind kind, double zoom)
    {
        if (kind == HandleKind.Rotate) return box.ToCanvas(0, -box.Height / 2 - RotateDistance / zoom);
        var (sx, sy) = Direction(kind);
        return box.ToCanvas(sx * box.Width / 2, sy * box.Height / 2);
    }

    /// <summary>
    /// Which corner point of a shape is under the point (while reshaping), or null. Points are the shape's current corners.
    /// </summary>
    public static int? VertexAt(LayerObject shape, PointD canvasPoint, double zoom)
    {
        var points = shape.ReshapePoints;
        if (points == null) return null;

        double radius = (HandleSize / 2 + 4) / zoom;
        int? best = null;
        double bestDistance = radius;
        for (int i = 0; i < points.Count; i++)
        {
            double d = shape.FromUnit(points[i]).DistanceTo(canvasPoint);
            if (d <= bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    /// <summary>Which handle (or the box itself) is under the point.</summary>
    public static HandleKind HitTest(ObjectBox box, LayerObject? obj, PointD point, double zoom)
    {
        double radius = (HandleSize / 2 + 3) / zoom;
        if (Position(box, HandleKind.Rotate, zoom).DistanceTo(point) <= radius + 2 / zoom) return HandleKind.Rotate;
        foreach (var kind in HandlesFor(obj))
            if (Position(box, kind, zoom).DistanceTo(point) <= radius) return kind;
        return box.Contains(point, (obj?.IsLineLike == true ? 6 : 2) / zoom) ? HandleKind.Move : HandleKind.None;
    }

    public static Cursor CursorFor(HandleKind kind, double rotation)
    {
        if (kind == HandleKind.Rotate) return Cursors.Hand;
        if (kind == HandleKind.Move) return Cursors.SizeAll;
        if (kind == HandleKind.None) return Cursors.Arrow;
        // Pick the resize cursor closest to the handle's direction after rotation.
        var (sx, sy) = Direction(kind);
        double angle = Math.Atan2(sy, sx) * 180 / Math.PI + rotation;
        int octant = (int)Math.Round(((angle % 180) + 180) % 180 / 45) % 4;
        return octant switch
        {
            0 => Cursors.SizeWE,
            1 => Cursors.SizeNWSE,
            2 => Cursors.SizeNS,
            _ => Cursors.SizeNESW,
        };
    }

    /// <summary>
    /// New box after dragging a resize handle to <paramref name="mouse"/>. The opposite side stays fixed.
    /// <paramref name="keepRatio"/> keeps the width/height ratio (corners only).
    /// </summary>
    public static ObjectBox Resize(ObjectBox start, HandleKind kind, PointD mouse, bool keepRatio)
    {
        var (sx, sy) = Direction(kind);
        var local = start.ToLocal(mouse);
        double w = start.Width, h = start.Height;
        if (sx != 0) w = Math.Max(1, sx * local.X + start.Width / 2);
        if (sy != 0) h = Math.Max(1, sy * local.Y + start.Height / 2);

        if (keepRatio && sx != 0 && sy != 0)
        {
            double scale = Math.Max(w / start.Width, h / start.Height);
            w = Math.Max(1, start.Width * scale);
            h = Math.Max(1, start.Height * scale);
        }

        // The fixed side stays where it was: shift the center by half the size change.
        double cx = sx * (w - start.Width) / 2, cy = sy * (h - start.Height) / 2;
        var center = start.ToCanvas(cx, cy);
        return ObjectBox.FromCenter(center, w, h, start.Rotation);
    }

    /// <summary>Rotation for the rotate handle at <paramref name="mouse"/>; Shift snaps to 15°.</summary>
    public static double RotationFor(ObjectBox box, PointD mouse, bool snap)
    {
        var c = box.Center;
        double angle = Math.Atan2(mouse.Y - c.Y, mouse.X - c.X) * 180 / Math.PI + 90;
        if (snap) angle = Math.Round(angle / 15) * 15;
        return ObjectBox.NormalizeAngle(angle);
    }
}
