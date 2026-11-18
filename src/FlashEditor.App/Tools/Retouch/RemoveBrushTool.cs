using System.Diagnostics;
using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>
/// Remove Brush (AI): paint over what you want gone (a person in the background, a logo, a blemish). When you let go of the mouse the AI
/// paints in what would plausibly be behind it, on a new "AI Fill" layer. While you paint, the area shows as a selection.
/// </summary>
public sealed class RemoveBrushTool : ToolBase
{
    private readonly BrushOptions _options = new() { Size = 30 };
    private byte[]? _mask;
    private PointD? _last;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastShown;

    public RemoveBrushTool(IToolHost host) : base(host)
    {
        _options.PropertyChanged += (_, _) => OnPropertyChanged(nameof(BrushPreviewSize));
    }

    public override string Name => "Remove Brush (AI)";
    public override string IconKey => "RemoveBrush";
    public override Key Shortcut => Key.J;
    public override object Options => _options;
    public override Cursor Cursor => Cursors.Cross;
    public override double BrushPreviewSize => _options.Size;

    public override void Activate()
        => Host.SetStatus("Remove Brush (AI): paint over what you want removed, then let go. The AI fills it in on a new layer. Ctrl+Z undoes it.");

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        var doc = Document;
        _mask = new byte[doc.Width * doc.Height];
        _last = null;
        Stamp(e.Position);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_mask == null) return;
        Stamp(e.Position);
        if (_clock.ElapsedMilliseconds - _lastShown > 80) Show();
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (_mask == null) return;
        Show();
        _mask = null;
        _last = null;
        Host.RunAiFill();
    }

    public override void Deactivate() { _mask = null; _last = null; }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.OemCloseBrackets: _options.Grow(); return true;
            case Key.OemOpenBrackets: _options.Shrink(); return true;
            default: return false;
        }
    }

    private void Show()
    {
        _lastShown = _clock.ElapsedMilliseconds;
        var doc = Document;
        if (_mask != null) doc.Selection = Selection.FromMask((byte[])_mask.Clone(), doc.Width, doc.Height);
    }

    // Paints a round dab, and fills in the line from the previous point so fast strokes leave no gaps.
    private void Stamp(PointD p)
    {
        var from = _last ?? p;
        double distance = Math.Sqrt((p.X - from.X) * (p.X - from.X) + (p.Y - from.Y) * (p.Y - from.Y));
        double step = Math.Max(1, _options.Size / 4);
        int count = Math.Max(1, (int)Math.Ceiling(distance / step));
        for (int i = 1; i <= count; i++)
        {
            double t = (double)i / count;
            Dab(from.X + (p.X - from.X) * t, from.Y + (p.Y - from.Y) * t);
        }
        _last = p;
    }

    private void Dab(double cx, double cy)
    {
        var doc = Document;
        double r = _options.Size / 2;
        int x0 = Math.Max(0, (int)Math.Floor(cx - r)), x1 = Math.Min(doc.Width - 1, (int)Math.Ceiling(cx + r));
        int y0 = Math.Max(0, (int)Math.Floor(cy - r)), y1 = Math.Min(doc.Height - 1, (int)Math.Ceiling(cy + r));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                if (dx * dx + dy * dy <= r * r) _mask![y * doc.Width + x] = 255;
            }
    }
}
