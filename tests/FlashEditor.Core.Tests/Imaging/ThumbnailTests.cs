using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Imaging;

public class ThumbnailTests
{
    [Fact]
    public void Thumbnail_keeps_the_aspect_ratio_and_never_grows_the_picture()
    {
        Assert.Equal((96, 54), Thumbnail.FitSize(1920, 1080, 96, 96));
        Assert.Equal((40, 20), Thumbnail.FitSize(40, 20, 96, 96));       // already small
        Assert.Equal((1, 96), Thumbnail.FitSize(1, 4000, 96, 96));       // extreme shapes stay valid
    }

    [Fact]
    public void Thumbnail_shows_where_the_content_is_and_its_color()
    {
        var picture = new PixelBuffer(400, 200);
        for (int y = 0; y < 100; y++)
            for (int x = 0; x < 200; x++)
                picture.SetPixel(x, y, new ColorRgba(255, 0, 0));       // red block in the top-left quarter

        var thumb = Thumbnail.Create(picture, 40, 40);
        Assert.Equal(40, thumb.Width);
        Assert.Equal(20, thumb.Height);

        var inside = thumb.GetPixel(5, 5);
        Assert.Equal(new ColorRgba(255, 0, 0), inside);
        Assert.Equal(0, thumb.GetPixel(30, 15).A);
    }

    [Fact]
    public void Half_transparent_edges_do_not_darken_the_color()
    {
        var picture = new PixelBuffer(10, 10);
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
                picture.SetPixel(x, y, x < 5 ? new ColorRgba(0, 255, 0) : ColorRgba.Transparent);

        var thumb = Thumbnail.Create(picture, 1, 1);
        var pixel = thumb.GetPixel(0, 0);
        Assert.Equal(255, pixel.G);                    // the green is still full green...
        Assert.InRange((int)pixel.A, 120, 135);        // ...just half as opaque
    }

    [Fact]
    public void Thumbnail_of_a_huge_picture_reads_only_a_few_pixels_per_thumbnail_pixel()
    {
        var picture = new PixelBuffer(6000, 4000);
        picture.Fill(new ColorRgba(10, 20, 30));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var thumb = Thumbnail.Create(picture, 96, 96);
        watch.Stop();
        Assert.Equal(new ColorRgba(10, 20, 30), thumb.GetPixel(50, 30));
        Assert.True(watch.ElapsedMilliseconds < 200, $"took {watch.ElapsedMilliseconds} ms");
    }
}
