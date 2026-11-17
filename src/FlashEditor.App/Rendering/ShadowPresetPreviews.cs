using System.Windows.Media.Imaging;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Rendering;

/// <summary>A shadow preset with a small picture of what it looks like (for the preset gallery).</summary>
public sealed record ShadowPresetItem(ShadowPreset Preset, BitmapSource Preview)
{
    public string Name => Preset.Name;
    public string Description => Preset.Description;
}

/// <summary>Draws the little pictures of the shadow presets with the real object renderer, so they always match the result.</summary>
public static class ShadowPresetPreviews
{
    private const int Size = 72;
    private const double ShadowScale = 0.45; // the sample square is much smaller than a real object: scale the shadow down with it

    public static IReadOnlyList<ShadowPresetItem> Create()
        => ShadowPreset.All.Select(preset => new ShadowPresetItem(preset, Render(preset))).ToList();

    private static BitmapSource Render(ShadowPreset preset)
    {
        var page = new PixelBuffer(Size, Size);
        page.Fill(new ColorRgba(238, 238, 242));

        var sample = new ShapeObject(new ObjectBox(Size * 0.5 - 17, Size * 0.5 - 17, 34, 34), ShapeKind.Rectangle,
            new ColorRgba(59, 130, 246), null, 0, CornerRadius: 7)
        {
            Shadow = preset.CreateStyle().Scaled(ShadowScale),
        };
        var drawn = ObjectRendering.Renderer.Render(sample, Size, Size);
        Compositor.DrawOver(page, drawn, 0, 0);
        return BitmapConverter.ToFrozenBitmap(page);
    }
}
