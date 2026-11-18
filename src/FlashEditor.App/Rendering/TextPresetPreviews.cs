using System.Windows.Media.Imaging;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Rendering;

/// <summary>A text design with a small picture of what it looks like (for the design gallery).</summary>
public sealed record TextPresetItem(TextPreset Preset, BitmapSource Preview)
{
    public string Name => Preset.Name;
    public string Description => Preset.Description;
}

/// <summary>Draws the little pictures of the text designs with the real object renderer, so they always match the result.</summary>
public static class TextPresetPreviews
{
    private const int Size = 72;
    private const double SampleSize = 34;

    public static IReadOnlyList<TextPresetItem> Create()
        => TextPreset.All.Select(preset => new TextPresetItem(preset, Render(preset))).ToList();

    private static BitmapSource Render(TextPreset preset)
    {
        var page = new PixelBuffer(Size, Size);
        page.Fill(preset.PreviewBackground ?? new ColorRgba(238, 238, 242));

        var sample = new TextObject(new ObjectBox(0, Size * 0.5 - 24, Size, 48), "Aa", "Segoe UI", SampleSize, ColorRgba.Black, Align: TextAlign.Center);
        var styled = preset.ApplyTo(sample);
        var drawn = ObjectRendering.Renderer.Render(styled, Size, Size);
        Compositor.DrawOver(page, drawn, 0, 0);
        return BitmapConverter.ToFrozenBitmap(page);
    }
}
