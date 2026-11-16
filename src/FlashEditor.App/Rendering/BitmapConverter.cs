using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Rendering;

/// <summary>Moves pixels between the core <see cref="PixelBuffer"/> and WPF bitmaps.</summary>
public static class BitmapConverter
{
    public static WriteableBitmap CreateWriteable(PixelBuffer pixels)
    {
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height, 96, 96, PixelFormats.Bgra32, null);
        Update(bitmap, pixels, pixels.Bounds);
        return bitmap;
    }

    /// <summary>Copies only the changed rectangle to the screen bitmap.</summary>
    public static void Update(WriteableBitmap bitmap, PixelBuffer pixels, PixelRect region)
    {
        region = region.Intersect(pixels.Bounds);
        if (region.IsEmpty) return;
        bitmap.WritePixels(new Int32Rect(region.X, region.Y, region.Width, region.Height),
            pixels.Data, pixels.Stride, region.X, region.Y);
    }

    public static BitmapSource ToFrozenBitmap(PixelBuffer pixels)
    {
        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Bgra32, null,
            pixels.Data, pixels.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    public static PixelBuffer FromBitmapSource(BitmapSource source)
    {
        BitmapSource converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var buffer = new PixelBuffer(converted.PixelWidth, converted.PixelHeight);
        converted.CopyPixels(buffer.Data, buffer.Stride, 0);
        return buffer;
    }

    /// <summary>Converts premultiplied BGRA (what WPF renders) into straight alpha.</summary>
    public static PixelBuffer FromPremultiplied(byte[] data, int width, int height)
    {
        var buffer = new PixelBuffer(width, height, data);
        for (int i = 0; i < data.Length; i += 4)
        {
            byte a = data[i + 3];
            if (a == 0 || a == 255) continue;
            double scale = 255.0 / a;
            data[i] = ColorMath.ClampByte(data[i] * scale);
            data[i + 1] = ColorMath.ClampByte(data[i + 1] * scale);
            data[i + 2] = ColorMath.ClampByte(data[i + 2] * scale);
        }
        return buffer;
    }

    public static Color ToWpf(ColorRgba c) => Color.FromArgb(c.A, c.R, c.G, c.B);
    public static ColorRgba FromWpf(Color c) => new(c.R, c.G, c.B, c.A);
}
