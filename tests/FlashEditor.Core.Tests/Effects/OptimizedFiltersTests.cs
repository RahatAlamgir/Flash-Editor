using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Effects;

/// <summary>
/// The blur and layer compositing were rewritten to be faster. These tests keep a copy of the
/// original, simple algorithms and require the fast versions to give exactly the same pixels.
/// </summary>
public class OptimizedFiltersTests
{
    private static PixelBuffer RandomImage(int w, int h, int seed, bool withTransparency)
    {
        var rnd = new Random(seed);
        var image = new PixelBuffer(w, h);
        rnd.NextBytes(image.Data);
        for (int i = 3; i < image.Data.Length; i += 4)
        {
            int roll = rnd.Next(10);
            image.Data[i] = !withTransparency ? (byte)255 : roll < 3 ? (byte)0 : roll < 6 ? (byte)255 : image.Data[i];
        }
        return image;
    }

    private static void AssertSame(PixelBuffer expected, PixelBuffer actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        int differing = 0;
        for (int i = 0; i < expected.Data.Length; i++)
            if (expected.Data[i] != actual.Data[i]) differing++;
        Assert.True(differing == 0, $"{differing} of {expected.Data.Length} bytes differ");
    }

    // ------------------------------------------------------------------ blur

    /// <summary>The original blur: one column at a time, one channel at a time.</summary>
    private static PixelBuffer ReferenceGaussian(PixelBuffer source, double sigma)
    {
        int w = source.Width, h = source.Height;
        var data = new float[w * h * 4];
        for (int i = 0; i < data.Length; i += 4)
        {
            float a = source.Data[i + 3] / 255f;
            data[i] = source.Data[i] * a; data[i + 1] = source.Data[i + 1] * a; data[i + 2] = source.Data[i + 2] * a;
            data[i + 3] = source.Data[i + 3];
        }
        var temp = new float[data.Length];
        foreach (int radius in Radii(sigma))
        {
            float scale = 1f / (2 * radius + 1);
            for (int y = 0; y < h; y++)
                for (int c = 0; c < 4; c++)
                {
                    float sum = 0; int row = y * w;
                    for (int k = -radius; k <= radius; k++) sum += data[(row + Math.Clamp(k, 0, w - 1)) * 4 + c];
                    for (int x = 0; x < w; x++)
                    {
                        temp[(row + x) * 4 + c] = sum * scale;
                        sum += data[(row + Math.Min(w - 1, x + radius + 1)) * 4 + c] - data[(row + Math.Max(0, x - radius)) * 4 + c];
                    }
                }
            for (int x = 0; x < w; x++)
                for (int c = 0; c < 4; c++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++) sum += temp[(Math.Clamp(k, 0, h - 1) * w + x) * 4 + c];
                    for (int y = 0; y < h; y++)
                    {
                        data[(y * w + x) * 4 + c] = sum * scale;
                        sum += temp[(Math.Min(h - 1, y + radius + 1) * w + x) * 4 + c] - temp[(Math.Max(0, y - radius) * w + x) * 4 + c];
                    }
                }
        }
        var result = new PixelBuffer(w, h);
        for (int i = 0; i < data.Length; i += 4)
        {
            float a = data[i + 3];
            if (a < 0.5f) continue;
            float inv = 255f / a;
            result.Data[i] = ColorMath.ClampByte(data[i] * inv); result.Data[i + 1] = ColorMath.ClampByte(data[i + 1] * inv);
            result.Data[i + 2] = ColorMath.ClampByte(data[i + 2] * inv); result.Data[i + 3] = ColorMath.ClampByte(a);
        }
        return result;
    }

    private static int[] Radii(double sigma)
    {
        const int passes = 3;
        double ideal = Math.Sqrt(12 * sigma * sigma / passes + 1);
        int lower = (int)Math.Floor(ideal);
        if (lower % 2 == 0) lower--;
        int upper = lower + 2;
        double mIdeal = (12 * sigma * sigma - passes * lower * lower - 4 * passes * lower - 3 * passes) / (-4.0 * lower - 4);
        int m = (int)Math.Round(mIdeal);
        return Enumerable.Range(0, passes).Select(i => ((i < m ? lower : upper) - 1) / 2).ToArray();
    }

    [Theory]
    [InlineData(97, 61, 0.8, false)]
    [InlineData(97, 61, 3.0, true)]
    [InlineData(131, 77, 12.0, true)]    // wider than the 128-pixel bands the fast version works in
    [InlineData(20, 300, 25.0, false)]   // blur radius larger than the picture is wide
    [InlineData(1, 1, 4.0, false)]
    public void Gaussian_blur_gives_exactly_the_original_result(int w, int h, double sigma, bool transparency)
    {
        var image = RandomImage(w, h, seed: w * 31 + h, transparency);
        AssertSame(ReferenceGaussian(image, sigma), Blur.Gaussian(image, sigma));
    }

    [Fact]
    public void Sharpen_and_Gaussian_blur_effects_still_work_through_the_fast_blur()
    {
        var image = RandomImage(64, 48, 5, withTransparency: true);
        var blur = new GaussianBlurEffect();
        AssertSame(ReferenceGaussian(image, 4), blur.Apply(image, blur.CreateDefaultValues()));
    }

    // ------------------------------------------------------------------ compositing

    private static void ReferenceDrawOver(PixelBuffer target, PixelBuffer source, double opacity, Selection? selection)
    {
        for (int y = 0; y < target.Height; y++)
            for (int x = 0; x < target.Width; x++)
            {
                double factor = opacity;
                if (selection != null)
                {
                    factor *= selection.Coverage(x, y);
                    if (factor <= 0) continue;
                }
                BlendMath.OverPixel(target.Data, target.IndexOf(x, y), source.Data, source.IndexOf(x, y), factor);
            }
    }

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(0.6, false)]
    [InlineData(1.0, true)]
    [InlineData(0.35, true)]
    public void Drawing_one_layer_over_another_gives_exactly_the_original_result(double opacity, bool useSelection)
    {
        var below = RandomImage(80, 50, 21, withTransparency: true);
        var above = RandomImage(80, 50, 22, withTransparency: true);
        var selection = useSelection ? Selection.FromRectangle(new PixelRect(10, 8, 40, 30), 80, 50) : null;

        var expected = below.Clone();
        ReferenceDrawOver(expected, above, opacity, selection);
        var actual = below.Clone();
        Compositor.DrawOver(actual, above, 0, 0, opacity, selection);

        AssertSame(expected, actual);
    }

    [Fact]
    public void Flattening_several_layers_gives_exactly_the_original_result()
    {
        var doc = new Document(70, 40);
        var layers = new[]
        {
            new Layer("a", RandomImage(70, 40, 1, false)) { Opacity = 1.0 },
            new Layer("b", RandomImage(70, 40, 2, true)) { Opacity = 0.5 },
            new Layer("c", RandomImage(70, 40, 3, true)) { Opacity = 1.0 },
            new Layer("d", RandomImage(70, 40, 4, true)) { Opacity = 0.8, IsVisible = false },
        };
        doc.SetLayers(layers, layers[0]);

        var expected = new PixelBuffer(70, 40);
        foreach (var layer in layers.Where(l => l.IsVisible))
            ReferenceDrawOver(expected, layer.Pixels, layer.Opacity, null);

        AssertSame(expected, Compositor.Flatten(doc));
    }
}
