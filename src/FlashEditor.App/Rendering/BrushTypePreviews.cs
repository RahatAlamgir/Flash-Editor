using System.Windows.Media.Imaging;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Rendering;

/// <summary>A brush type with a picture of a stroke made with it (for the brush type picker).</summary>
public sealed record BrushTypeItem(BrushType Type, BitmapSource Preview)
{
    public string Name => Type.ToString();
    public string Description => BrushTypes.Description(Type);
}

/// <summary>Paints a short wavy stroke for each brush type with the real brush engine, so the pictures always match the result.</summary>
public static class BrushTypePreviews
{
    private const int Width = 120, Height = 34;

    private static IReadOnlyList<BrushTypeItem>? _items;

    /// <summary>All brush types with their pictures (made once, the first time they are needed).</summary>
    public static IReadOnlyList<BrushTypeItem> Items
        => _items ??= Enum.GetValues<BrushType>().Select(type => new BrushTypeItem(type, Render(type))).ToList();

    private static BitmapSource Render(BrushType type)
    {
        var page = new PixelBuffer(Width, Height);
        page.Fill(new ColorRgba(244, 244, 247));

        var stroke = new BrushStroke(page, new BrushSettings(Size: 14, Hardness: 0.8, Opacity: 1.0, type),
                                     new ColorRgba(35, 40, 60), erase: false, selection: null);
        for (int i = 0; i <= 60; i++)
        {
            double t = i / 60.0;
            stroke.AddPoint(new PointD(12 + t * (Width - 24), Height / 2.0 + Math.Sin(t * Math.PI * 2) * 8));
        }
        return BitmapConverter.ToFrozenBitmap(page);
    }
}
