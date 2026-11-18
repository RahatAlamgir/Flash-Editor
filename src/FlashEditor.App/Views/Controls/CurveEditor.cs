using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.Documents;

namespace FlashEditor.App.Views.Controls;

/// <summary>
/// The tone curve of a Curves adjustment layer: the left-to-right line says what goes in (dark to light), the height says what comes out.
/// Click on the curve to add a point, drag a point to bend the curve, double-click or right-click a point to remove it.
/// The curve changes live while you drag; releasing the mouse makes one undo step.
/// </summary>
public sealed class CurveEditor : FrameworkElement
{
    private const double PlotMargin = 6;
    private LayerViewModel? _layer;
    private int _dragging = -1;
    private IReadOnlyList<CurvePoint>? _dragStartPoints;

    /// <summary>The adjustment layer whose curve is shown (bind it to the active layer).</summary>
    public static readonly DependencyProperty LayerProperty = DependencyProperty.Register(
        nameof(Layer), typeof(LayerViewModel), typeof(CurveEditor), new PropertyMetadata(null, (d, e) => ((CurveEditor)d).Hook((LayerViewModel?)e.OldValue, (LayerViewModel?)e.NewValue)));

    public LayerViewModel? Layer { get => (LayerViewModel?)GetValue(LayerProperty); set => SetValue(LayerProperty, value); }

    public CurveEditor()
    {
        Width = 190;
        Height = 190;
        Cursor = Cursors.Cross;
        Focusable = false;
    }

    private void Hook(LayerViewModel? old, LayerViewModel? layer)
    {
        if (old != null) old.PropertyChanged -= OnLayerChanged;
        _layer = layer;
        if (layer != null) layer.PropertyChanged += OnLayerChanged;
        _dragging = -1;
        InvalidateVisual();
    }

    private void OnLayerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayerViewModel.CurvePoints) or nameof(LayerViewModel.CurveChannel) or nameof(LayerViewModel.IsAdjustment)) InvalidateVisual();
    }

    private IReadOnlyList<CurvePoint> Points => _layer?.CurvePoints ?? CurveMath.Straight;

    private Rect Plot => new(PlotMargin, PlotMargin, Math.Max(10, ActualWidth - 2 * PlotMargin), Math.Max(10, ActualHeight - 2 * PlotMargin));

    private Point ToScreen(CurvePoint p)
    {
        var plot = Plot;
        return new Point(plot.Left + p.X / 255 * plot.Width, plot.Bottom - p.Y / 255 * plot.Height);
    }

    private CurvePoint FromScreen(Point p)
    {
        var plot = Plot;
        return new CurvePoint(Math.Clamp((p.X - plot.Left) / plot.Width * 255, 0, 255), Math.Clamp((plot.Bottom - p.Y) / plot.Height * 255, 0, 255));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var plot = Plot;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1b, 0x1b, 0x1b)), new Pen(new SolidColorBrush(Color.FromRgb(0x4a, 0x4a, 0x4a)), 1), plot);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), 1);
        for (int i = 1; i < 4; i++)
        {
            double x = plot.Left + plot.Width * i / 4, y = plot.Top + plot.Height * i / 4;
            dc.DrawLine(grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            dc.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
        }
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), 1) { DashStyle = DashStyles.Dash }, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Top));

        var color = (_layer?.CurveChannel ?? 0) switch
        {
            1 => Color.FromRgb(0xff, 0x5a, 0x5a),
            2 => Color.FromRgb(0x4a, 0xde, 0x80),
            3 => Color.FromRgb(0x5a, 0x9b, 0xff),
            _ => Color.FromRgb(0xe6, 0xe6, 0xe6),
        };
        var brush = new SolidColorBrush(color);
        var points = Points;
        var table = CurveMath.BuildTable(points);
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(ToScreen(new CurvePoint(0, table[0])), false, false);
            for (int x = 1; x < 256; x++) ctx.LineTo(ToScreen(new CurvePoint(x, table[x])), true, true);
        }
        line.Freeze();
        dc.DrawGeometry(null, new Pen(brush, 2) { LineJoin = PenLineJoin.Round }, line);

        for (int i = 0; i < points.Count; i++)
            dc.DrawEllipse(i == _dragging ? brush : new SolidColorBrush(Color.FromRgb(0x1b, 0x1b, 0x1b)), new Pen(brush, 1.6), ToScreen(points[i]), 4.5, 4.5);
    }

    private int NearestPoint(Point at)
    {
        var points = Points;
        int best = -1;
        double bestDistance = 10;
        for (int i = 0; i < points.Count; i++)
        {
            var p = ToScreen(points[i]);
            double d = Math.Sqrt((p.X - at.X) * (p.X - at.X) + (p.Y - at.Y) * (p.Y - at.Y));
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_layer == null) return;
        var at = e.GetPosition(this);
        int index = NearestPoint(at);
        _dragStartPoints = _layer.CurvePoints;
        if (e.ClickCount == 2 && index >= 0)
        {
            // double-click removes a point
            _layer.SetCurve(CurveMath.WithoutPoint(Points, index), commit: true, before: _dragStartPoints);
            return;
        }
        if (index < 0)
        {
            var added = FromScreen(at);
            var next = CurveMath.WithPoint(Points, added);
            _layer.SetCurve(next, commit: false);
            index = NearestPoint(at);
        }
        _dragging = index;
        CaptureMouse();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging < 0 || _layer == null || e.LeftButton != MouseButtonState.Pressed) return;
        var points = Points.ToList();
        if (_dragging >= points.Count) return;
        var p = FromScreen(e.GetPosition(this));
        double x = _dragging == 0 ? 0 : _dragging == points.Count - 1 ? 255 : Math.Clamp(p.X, points[_dragging - 1].X + 4, points[_dragging + 1].X - 4);
        points[_dragging] = new CurvePoint(Math.Round(x), Math.Round(p.Y));
        _layer.SetCurve(points, commit: false);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging < 0 || _layer == null) return;
        _dragging = -1;
        ReleaseMouseCapture();
        _layer.SetCurve(Points, commit: true, before: _dragStartPoints);
        InvalidateVisual();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        if (_layer == null) return;
        int index = NearestPoint(e.GetPosition(this));
        if (index > 0 && index < Points.Count - 1)
            _layer.SetCurve(CurveMath.WithoutPoint(Points, index), commit: true, before: _layer.CurvePoints);
        e.Handled = true;
    }

    protected override Size MeasureOverride(Size availableSize) => new(Width, Height);
}
