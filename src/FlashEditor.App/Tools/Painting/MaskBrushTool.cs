using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Painting;

/// <summary>
/// Paints on the layer mask of the active layer: the foreground color decides what happens (black hides what is under the brush, white
/// shows it, a grey is half way; X swaps black and white). The pixels of the layer are never changed, so a hidden part can always be
/// brought back. A layer without a mask gets one (everything shown) on the first stroke.
/// </summary>
public sealed class MaskBrushTool : ToolBase
{
    private readonly BrushOptions _options = new();
    private Layer? _layer;
    private BrushStroke? _stroke;
    private PixelBuffer? _proxy;        // the mask as a grey picture: the brush paints on this
    private byte[]? _original;          // the mask before the stroke (for undo)
    private PointD? _lastStrokeEnd;

    public MaskBrushTool(IToolHost host) : base(host)
    {
        _options.PropertyChanged += (_, _) => OnPropertyChanged(nameof(BrushPreviewSize));
    }

    public override string Name => "Mask Brush";
    public override string IconKey => "MaskBrush";
    public override Key Shortcut => Key.K;
    public override object Options => _options;
    public override Cursor Cursor => Cursors.Cross;
    public override double BrushPreviewSize => _options.Size;

    public override void Activate()
        => Host.SetStatus("Mask Brush: paint with black to hide parts of the layer and with white to bring them back (X swaps the colors). The layer's pixels are not changed.");

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        var layer = Document.ActiveLayer;
        if (layer == null) { Host.SetStatus("Select a layer first."); return; }
        if (layer.IsAdjustment) { Host.SetStatus("An adjustment layer has no pixels to mask."); return; }
        if (!layer.IsVisible) { Host.SetStatus("The active layer is hidden. Show it (eye icon) to paint on its mask."); return; }

        if (!layer.HasMask)
            Push(MaskStateAction.Record("Add Layer Mask", layer, () => layer.SetMask(new LayerMask(layer.Width, layer.Height))));
        if (!layer.MaskEnabled) layer.MaskEnabled = true;

        var mask = layer.Mask!;
        _layer = layer;
        _original = (byte[])mask.Alpha.Clone();
        _proxy = mask.ToGrayPicture();

        var c = Host.Colors.Foreground;
        byte grey = (byte)Math.Clamp((int)Math.Round(0.299 * c.R + 0.587 * c.G + 0.114 * c.B), 0, 255);
        _stroke = new BrushStroke(_proxy, _options.ToSettings(), new ColorRgba(grey, grey, grey), false, Document.Selection);

        if (e.Shift && _lastStrokeEnd is PointD previous) Sync(_stroke.AddPoint(previous));
        Sync(_stroke.AddPoint(e.Position));
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_stroke == null) return;
        Sync(_stroke.AddPoint(e.Position));
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (_stroke == null) return;
        _lastStrokeEnd = e.Position;
        Finish();
    }

    public override void Deactivate() => Finish();

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.OemCloseBrackets: _options.Grow(); return true;
            case Key.OemOpenBrackets: _options.Shrink(); return true;
            default: return false;
        }
    }

    // Copies what the brush painted from the grey picture into the mask and shows it.
    private void Sync(PixelRect dirty)
    {
        if (_layer?.Mask is not { } mask || _proxy == null || dirty.IsEmpty) return;
        dirty = dirty.Intersect(mask.Bounds);
        for (int y = dirty.Y; y < dirty.Bottom; y++)
            for (int x = dirty.X; x < dirty.Right; x++)
                mask.Alpha[y * mask.Width + x] = _proxy.Data[(y * _proxy.Width + x) * 4 + 1];
        _layer.NotifyMaskChanged(dirty);
    }

    private void Finish()
    {
        if (_stroke != null && _layer?.Mask is { } mask && _original != null)
        {
            var region = _stroke.TotalDirty.Intersect(mask.Bounds);
            if (!region.IsEmpty)
            {
                var before = new byte[region.Width * region.Height];
                for (int y = 0; y < region.Height; y++)
                    Array.Copy(_original, (region.Y + y) * mask.Width + region.X, before, y * region.Width, region.Width);
                Push(new MaskPaintAction(Name, _layer, region, before, mask.CopyRegion(region)));
            }
        }
        _stroke = null;
        _layer = null;
        _proxy = null;
        _original = null;
    }
}
