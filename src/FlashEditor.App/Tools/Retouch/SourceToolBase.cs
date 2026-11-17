using System.Windows;
using System.Windows.Media;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>
/// Clone Stamp and Healing Brush: Alt+click chooses the place to copy from, then painting copies from there. A small cross
/// on the canvas shows where the copy comes from.
/// </summary>
public abstract class SourceToolBase : RetouchToolBase
{
    private PointD? _source;                 // the place chosen with Alt+click
    private (int Dx, int Dy)? _offset;       // distance from the painted place to the copied place

    protected SourceToolBase(IToolHost host, RetouchOptions options) : base(host, options) { }

    /// <summary>The operation that copies from <paramref name="dx"/>, <paramref name="dy"/> away.</summary>
    protected abstract IRetouchOp CreateOperation(int dx, int dy);

    protected override bool PreviewClick(ToolMouseArgs e)
    {
        if (!e.Alt) return false;
        _source = e.Position;
        _offset = null;
        ShowCross(e.Position);
        Host.SetStatus("Copy-from point chosen. Now paint where it should be copied to.");
        return true;
    }

    protected override IRetouchOp? BeginOperation(ToolMouseArgs e, Layer layer)
    {
        if (_source is not PointD source)
        {
            Host.SetStatus("Alt+click first, on the place you want to copy from.");
            return null;
        }
        if (_offset == null || !Settings.Aligned)
            _offset = ((int)Math.Round(source.X - e.Position.X), (int)Math.Round(source.Y - e.Position.Y));
        return CreateOperation(_offset.Value.Dx, _offset.Value.Dy);
    }

    protected override void StrokeMoved(ToolMouseArgs e)
    {
        if (_offset is { } offset) ShowCross(new PointD(e.Position.X + offset.Dx, e.Position.Y + offset.Dy));
    }

    protected override void StrokeEnded()
    {
        if (_source is PointD source && Settings.Aligned == false) ShowCross(source);
        else Host.Canvas?.ShowSelectionPreview(null);
    }

    /// <summary>A small cross marking the place that is being copied.</summary>
    private void ShowCross(PointD at)
    {
        double arm = 8 / Math.Max(0.05, Host.Canvas?.Zoom ?? 1);
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(new Point(at.X - arm, at.Y), new Point(at.X + arm, at.Y)));
        geometry.Children.Add(new LineGeometry(new Point(at.X, at.Y - arm), new Point(at.X, at.Y + arm)));
        geometry.Freeze();
        Host.Canvas?.ShowSelectionPreview(geometry);
    }
}
