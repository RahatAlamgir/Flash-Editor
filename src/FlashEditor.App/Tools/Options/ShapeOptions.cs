using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Tools.Options;

/// <summary>Settings of the Shapes tool: which shape, fill, border. They apply to the next shape drawn.</summary>
public sealed class ShapeOptions : ObservableObject
{
    private ShapeKind _kind = ShapeKind.Rectangle;
    private bool _fillEnabled = true;
    private ColorRgba _fillColor = new(59, 130, 246);
    private bool _strokeEnabled;
    private ColorRgba _strokeColor = ColorRgba.Black;
    private double _strokeWidth = 4;
    private double _cornerRadius;
    private int _points = 5;
    private double _inner = 0.5;
    private double _sweep = 270;

    public ShapeOptions(IDialogService dialogs)
    {
        PickFillCommand = new RelayCommand(() => { if (dialogs.PickColor(FillColor, "Fill color") is { } c) { FillColor = c; FillEnabled = true; } });
        PickStrokeCommand = new RelayCommand(() => { if (dialogs.PickColor(StrokeColor, "Border color") is { } c) { StrokeColor = c; StrokeEnabled = true; } });
        SelectKindCommand = new RelayCommand(p => { if (p is ShapeKind k) Kind = k; });
    }

    public IReadOnlyList<ShapeKind> Kinds { get; } = Enum.GetValues<ShapeKind>();

    public ShapeKind Kind
    {
        get => _kind;
        set
        {
            if (!SetField(ref _kind, value)) return;
            if (value == ShapeKind.Line && !StrokeEnabled) StrokeEnabled = true; // a line is only a border
            if (value is ShapeKind.Star && Points < 3) Points = 5;
            // A pie starts solid (no hole); the other ring-like shapes start with a half-size hole.
            if (value == ShapeKind.Pie) Inner = 0; else if (Inner < 0.05) Inner = 0.5;
            OnPropertyChanged(nameof(HasInner));
            OnPropertyChanged(nameof(HasSweep));
            OnPropertyChanged(nameof(InnerLabel));
            OnPropertyChanged(nameof(HasCorners));
            OnPropertyChanged(nameof(HasPoints));
        }
    }

    public bool HasCorners => Kind is ShapeKind.Rectangle or ShapeKind.SpeechBubble or ShapeKind.Frame;
    public bool HasInner => ShapeNames.HasInner(Kind);
    public bool HasSweep => Kind == ShapeKind.Pie;
    public string InnerLabel => ShapeNames.InnerLabel(Kind);

    /// <summary>Hole size / arm thickness / bite, 0..95 %.</summary>
    public double InnerPercent { get => Math.Round(_inner * 100); set => Inner = Math.Clamp(value, 0, 95) / 100; }
    public double Inner { get => _inner; set { if (SetField(ref _inner, Math.Clamp(value, 0, 0.95))) OnPropertyChanged(nameof(InnerPercent)); } }
    public double Sweep { get => _sweep; set => SetField(ref _sweep, Math.Clamp(Math.Round(value), 10, 360)); }
    public bool HasPoints => Kind is ShapeKind.Star or ShapeKind.Polygon;

    public bool FillEnabled { get => _fillEnabled; set => SetField(ref _fillEnabled, value); }
    public ColorRgba FillColor { get => _fillColor; set { if (SetField(ref _fillColor, value)) OnPropertyChanged(nameof(FillBrush)); } }
    public Brush FillBrush => new SolidColorBrush(BitmapConverter.ToWpf(FillColor));

    public bool StrokeEnabled { get => _strokeEnabled; set => SetField(ref _strokeEnabled, value); }
    public ColorRgba StrokeColor { get => _strokeColor; set { if (SetField(ref _strokeColor, value)) OnPropertyChanged(nameof(StrokeBrush)); } }
    public Brush StrokeBrush => new SolidColorBrush(BitmapConverter.ToWpf(StrokeColor));

    public double StrokeWidth { get => _strokeWidth; set => SetField(ref _strokeWidth, Math.Clamp(Math.Round(value), 1, 200)); }
    public double CornerRadius { get => _cornerRadius; set => SetField(ref _cornerRadius, Math.Clamp(Math.Round(value), 0, 500)); }
    public double Points { get => _points; set => SetField(ref _points, (int)Math.Clamp(Math.Round(value), 3, 24)); }

    public RelayCommand PickFillCommand { get; }
    public RelayCommand PickStrokeCommand { get; }
    public RelayCommand SelectKindCommand { get; }

    /// <summary>The shape these settings describe, in the given box.</summary>
    public ShapeObject Create(ObjectBox box)
    {
        bool line = Kind == ShapeKind.Line;
        return new ShapeObject(box, Kind,
            Fill: line ? null : FillEnabled ? FillColor : null,
            Stroke: line || StrokeEnabled || !FillEnabled ? StrokeColor : null,
            StrokeWidth: StrokeWidth,
            CornerRadius: CornerRadius,
            Points: _points,
            Inner: _inner,
            Sweep: _sweep);
    }
}
