using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.Core.Painting;

/// <summary>What the Liquify brush does to the pixels under it.</summary>
public enum LiquifyMode
{
    /// <summary>Pushes the pixels along in the direction the brush moves (like smearing wet paint).</summary>
    Push,

    /// <summary>Makes what is under the brush bigger (swells it from the middle).</summary>
    Bloat,

    /// <summary>Makes what is under the brush smaller (pinches it towards the middle).</summary>
    Pucker,
}

/// <summary>Liquify: bends, swells and pinches a picture under a soft round brush, with smooth edges.</summary>
public static class Liquify
{
    /// <summary>
    /// Applies the brush once, centered on (<paramref name="cx"/>, <paramref name="cy"/>). For <see cref="LiquifyMode.Push"/> (<paramref name="dx"/>,
    /// <paramref name="dy"/>) is how far the brush has just moved. Strength is 0..1. Returns the changed rectangle.
    /// </summary>
    public static PixelRect Apply(PixelBuffer target, double cx, double cy, double radius, double strength, LiquifyMode mode, double dx = 0, double dy = 0)
    {
        if (radius < 1 || strength <= 0) return PixelRect.Empty;
        var area = new PixelRect((int)Math.Floor(cx - radius) - 1, (int)Math.Floor(cy - radius) - 1, (int)Math.Ceiling(radius * 2) + 3, (int)Math.Ceiling(radius * 2) + 3)
            .Intersect(target.Bounds);
        if (area.IsEmpty) return PixelRect.Empty;
        if (mode == LiquifyMode.Push && Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) return PixelRect.Empty;

        var source = target.Crop(area);                         // what the pixels looked like before this step
        var data = target.Data;
        double r2 = radius * radius;

        Parallel.For(area.Y, area.Bottom, y =>
        {
            double py = y + 0.5 - cy;
            for (int x = area.X; x < area.Right; x++)
            {
                double px = x + 0.5 - cx;
                double d2 = px * px + py * py;
                if (d2 >= r2) continue;
                double t = 1 - d2 / r2;
                double falloff = t * t * strength;              // 1 in the middle, smoothly down to 0 at the edge

                double sx, sy;
                switch (mode)
                {
                    case LiquifyMode.Push:
                        sx = x + 0.5 - dx * falloff;
                        sy = y + 0.5 - dy * falloff;
                        break;
                    case LiquifyMode.Bloat:
                        sx = x + 0.5 - px * falloff * 0.35;
                        sy = y + 0.5 - py * falloff * 0.35;
                        break;
                    default:
                        sx = x + 0.5 + px * falloff * 0.35;
                        sy = y + 0.5 + py * falloff * 0.35;
                        break;
                }
                Resampler.SampleBilinear(source, Math.Clamp(sx - area.X - 0.5, 0, source.Width - 1), Math.Clamp(sy - area.Y - 0.5, 0, source.Height - 1),
                    data, target.IndexOf(x, y));
            }
        });
        return area;
    }
}
