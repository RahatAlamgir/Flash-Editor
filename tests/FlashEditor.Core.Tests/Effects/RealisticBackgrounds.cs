using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Effects;

/// <summary>
/// Photo-like test pictures for background removal: backgrounds that are NOT one flat color
/// (gradients, shadows, sensor/JPEG noise, texture), as in real photos.
/// Each returns the picture plus a mask of which pixels belong to the subject.
/// </summary>
internal static class RealisticBackgrounds
{
    public sealed record Scene(string Name, PixelBuffer Image, bool[] IsSubject);

    public static IEnumerable<Scene> All()
    {
        yield return StudioGradient();
        yield return NoisyBackdropWithShadow();
        yield return ColoredWallSubjectTouchingBottom();
        yield return DarkVignetteBackground();
    }

    /// <summary>Light-gray studio sweep: top 225 to bottom 165, plus mild noise. Red product in the middle.</summary>
    public static Scene StudioGradient()
    {
        int w = 320, h = 240;
        var rng = new Random(1);
        return Build("studio gradient", w, h,
            (x, y) => { double v = 225 - 60.0 * y / h + Noise(rng, 3); return (v, v, v + 4); },
            (x, y) => Ellipse(x, y, 160, 120, 60, 80),
            (x, y) => (200 - y * 0.2, 40, 35));
    }

    /// <summary>Beige backdrop with strong noise and a soft shadow under a dark-blue subject.</summary>
    public static Scene NoisyBackdropWithShadow()
    {
        int w = 320, h = 240;
        var rng = new Random(2);
        return Build("noisy backdrop + shadow", w, h,
            (x, y) =>
            {
                double shadow = Math.Exp(-(Math.Pow((x - 175) / 70.0, 2) + Math.Pow((y - 200) / 18.0, 2))) * 45;
                return (215 - shadow + Noise(rng, 8), 200 - shadow + Noise(rng, 8), 175 - shadow + Noise(rng, 8));
            },
            (x, y) => x >= 110 && x < 220 && y >= 60 && y < 190,
            (x, y) => (30, 45, 110 + x * 0.1));
    }

    /// <summary>Blue-green wall with a left-to-right gradient; a person-like shape touches the bottom edge.</summary>
    public static Scene ColoredWallSubjectTouchingBottom()
    {
        int w = 320, h = 240;
        var rng = new Random(3);
        return Build("colored wall, subject touches bottom", w, h,
            (x, y) => (70 + 50.0 * x / w + Noise(rng, 4), 140 + 30.0 * x / w + Noise(rng, 4), 150 + Noise(rng, 4)),
            (x, y) => Ellipse(x, y, 160, 90, 35, 45) || (y >= 130 && Math.Abs(x - 160) < 80 - (y - 130) * -0.2),
            (x, y) => y < 135 ? (225, 180, 150) : (180, 40, 60));
    }

    /// <summary>Dark background with a vignette (brighter middle) and a yellow subject.</summary>
    public static Scene DarkVignetteBackground()
    {
        int w = 320, h = 240;
        var rng = new Random(4);
        return Build("dark vignette", w, h,
            (x, y) =>
            {
                double d = Math.Sqrt(Math.Pow((x - 160) / 160.0, 2) + Math.Pow((y - 120) / 120.0, 2));
                double v = 70 - 45 * d + Noise(rng, 3);
                return (v, v, v + 6);
            },
            (x, y) => Ellipse(x, y, 160, 120, 70, 70),
            (x, y) => (235, 200, 40));
    }

    private static bool Ellipse(int x, int y, double cx, double cy, double rx, double ry)
        => Math.Pow((x - cx) / rx, 2) + Math.Pow((y - cy) / ry, 2) <= 1;

    private static double Noise(Random rng, double amount) => (rng.NextDouble() * 2 - 1) * amount;

    private static Scene Build(string name, int w, int h,
        Func<int, int, (double R, double G, double B)> background,
        Func<int, int, bool> isSubject,
        Func<int, int, (double R, double G, double B)> subject)
    {
        var image = new PixelBuffer(w, h);
        var mask = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool s = isSubject(x, y);
                mask[y * w + x] = s;
                var (r, g, b) = s ? subject(x, y) : background(x, y);
                image.SetPixel(x, y, new ColorRgba(ColorMath.ClampByte(r), ColorMath.ClampByte(g), ColorMath.ClampByte(b)));
            }
        }
        return new Scene(name, image, mask);
    }

    /// <summary>Share of background pixels made (mostly) transparent, and share of subject pixels kept opaque.</summary>
    public static (double BackgroundRemoved, double SubjectKept) Score(Scene scene, PixelBuffer result)
    {
        long bg = 0, bgRemoved = 0, subj = 0, subjKept = 0;
        int w = result.Width;
        for (int p = 0; p < scene.IsSubject.Length; p++)
        {
            int x = p % w, y = p / w;
            // Ignore a 2px band around the subject edge (anti-aliasing zone).
            if (NearEdge(scene, x, y, w, result.Height)) continue;
            byte a = result.GetPixel(x, y).A;
            if (scene.IsSubject[p]) { subj++; if (a >= 200) subjKept++; }
            else { bg++; if (a <= 55) bgRemoved++; }
        }
        return ((double)bgRemoved / bg, (double)subjKept / subj);
    }

    private static bool NearEdge(Scene scene, int x, int y, int w, int h)
    {
        bool self = scene.IsSubject[y * w + x];
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                if (scene.IsSubject[ny * w + nx] != self) return true;
            }
        return false;
    }
}
