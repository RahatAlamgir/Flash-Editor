using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Transforms;

/// <summary>Straightening a tilted picture and cutting out a flat rectangle from a picture taken at an angle (a page, a screen, a sign).</summary>
public static class PerspectiveTools
{
    /// <summary>
    /// The biggest upright rectangle that fits inside a picture of <paramref name="width"/> x <paramref name="height"/> that was turned by
    /// <paramref name="degrees"/> around its middle (so no empty corners are left). The rectangle has the same middle as the picture.
    /// </summary>
    public static (int Width, int Height) LargestInsideRotated(int width, int height, double degrees)
    {
        double a = Math.Abs(degrees % 180) * Math.PI / 180;
        if (a > Math.PI / 2) a = Math.PI - a;
        double sin = Math.Sin(a), cos = Math.Cos(a);
        if (sin < 1e-9) return (width, height);

        bool widthIsLonger = width >= height;
        double longSide = widthIsLonger ? width : height, shortSide = widthIsLonger ? height : width;
        double w, h;
        if (shortSide <= 2 * sin * cos * longSide || Math.Abs(sin - cos) < 1e-10)
        {
            // The rectangle touches the long sides of the turned picture with two corners.
            double x = 0.5 * shortSide;
            (w, h) = widthIsLonger ? (x / sin, x / cos) : (x / cos, x / sin);
        }
        else
        {
            double cos2 = cos * cos - sin * sin;
            w = (width * cos - height * sin) / cos2;
            h = (height * cos - width * sin) / cos2;
        }
        return (Math.Max(1, (int)Math.Floor(w)), Math.Max(1, (int)Math.Floor(h)));
    }

    /// <summary>Turns the picture by <paramref name="degrees"/> (clockwise) and, when <paramref name="cropCorners"/>, cuts away the empty corners.</summary>
    public static PixelBuffer Straighten(PixelBuffer source, double degrees, bool cropCorners)
    {
        var turned = ImageTransforms.Rotate(source, degrees);
        if (!cropCorners) return turned;
        var (w, h) = LargestInsideRotated(source.Width, source.Height, degrees);
        // Two pixels less on every side: the very edge of a turned picture is soft (partly see-through).
        w = Math.Max(1, Math.Min(w, turned.Width) - 4);
        h = Math.Max(1, Math.Min(h, turned.Height) - 4);
        return turned.Crop(new PixelRect((turned.Width - w) / 2, (turned.Height - h) / 2, w, h));
    }

    /// <summary>The size of a flat rectangle that suits the four corners (top-left, top-right, bottom-right, bottom-left): the average length of opposite edges.</summary>
    public static (int Width, int Height) SuggestedSize(IReadOnlyList<PointD> quad)
    {
        double Distance(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        double w = (Distance(quad[0], quad[1]) + Distance(quad[3], quad[2])) / 2;
        double h = (Distance(quad[0], quad[3]) + Distance(quad[1], quad[2])) / 2;
        return (Math.Max(1, (int)Math.Round(w)), Math.Max(1, (int)Math.Round(h)));
    }

    /// <summary>
    /// Cuts the four-sided area <paramref name="quad"/> (top-left, top-right, bottom-right, bottom-left, in pixels of the source) out of the
    /// picture and makes it a flat <paramref name="width"/> x <paramref name="height"/> rectangle (smooth, bilinear). Null when the four points do
    /// not make a usable shape.
    /// </summary>
    public static PixelBuffer? PerspectiveCrop(PixelBuffer source, IReadOnlyList<PointD> quad, int width, int height)
    {
        if (quad.Count != 4 || width < 1 || height < 1) return null;
        double x0 = quad[0].X, y0 = quad[0].Y, x1 = quad[1].X, y1 = quad[1].Y, x2 = quad[2].X, y2 = quad[2].Y, x3 = quad[3].X, y3 = quad[3].Y;
        double dx1 = x1 - x2, dx2 = x3 - x2, dx3 = x0 - x1 + x2 - x3;
        double dy1 = y1 - y2, dy2 = y3 - y2, dy3 = y0 - y1 + y2 - y3;
        double g, h;
        if (Math.Abs(dx3) < 1e-12 && Math.Abs(dy3) < 1e-12) { g = 0; h = 0; }
        else
        {
            double denominator = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denominator) < 1e-12) return null;
            g = (dx3 * dy2 - dy3 * dx2) / denominator;
            h = (dx1 * dy3 - dy1 * dx3) / denominator;
        }
        // The map from the unit square to the quad: (x, y) = (a u + b v + c, d u + e v + f) / (g u + h v + 1)
        double a = x1 - x0 + g * x1, b = x3 - x0 + h * x3, c = x0;
        double d = y1 - y0 + g * y1, e = y3 - y0 + h * y3, f = y0;

        var result = new PixelBuffer(width, height);
        Parallel.For(0, height, py =>
        {
            double v = (py + 0.5) / height;
            for (int px = 0; px < width; px++)
            {
                double u = (px + 0.5) / width;
                double w = g * u + h * v + 1;
                if (Math.Abs(w) < 1e-12) continue;
                Resampler.SampleBilinear(source, (a * u + b * v + c) / w - 0.5, (d * u + e * v + f) / w - 0.5, result.Data, result.IndexOf(px, py));
            }
        });
        return result;
    }
}
