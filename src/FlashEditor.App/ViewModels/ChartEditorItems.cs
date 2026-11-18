using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels;

/// <summary>One row of the chart's number table in Properties: its color, name and number.</summary>
public sealed class ChartRowViewModel : ObservableObject
{
    private readonly ObjectPropertiesViewModel _owner;
    private string _label;
    private string _valueText;
    private Brush _brush;

    public ChartRowViewModel(ObjectPropertiesViewModel owner, int index, ChartItem item, ColorRgba color)
    {
        _owner = owner;
        Index = index;
        _label = item.Label;
        _valueText = ChartData.Format(new[] { item }).Split(',')[^1].Trim();
        _brush = ToBrush(color);
        Value = item.Value;
        PickColorCommand = new RelayCommand(() => _owner.PickChartRowColor(Index));
        RemoveCommand = new RelayCommand(() => _owner.RemoveChartRow(Index));
    }

    public int Index { get; set; }
    public double Value { get; private set; }

    public string Label
    {
        get => _label;
        set { if (SetField(ref _label, value ?? "")) _owner.SetChartRow(Index, _label, null); }
    }

    /// <summary>The number as typed (a wrong entry is ignored and the old number comes back).</summary>
    public string ValueText
    {
        get => _valueText;
        set
        {
            if (!double.TryParse((value ?? "").Trim().Replace(" ", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)
                || double.IsNaN(v) || double.IsInfinity(v))
            {
                OnPropertyChanged();                       // shows the old number again
                return;
            }
            if (SetField(ref _valueText, value!.Trim())) { Value = v; _owner.SetChartRow(Index, null, v); }
        }
    }

    public Brush ColorBrush { get => _brush; private set => SetField(ref _brush, value); }
    public void SetColor(ColorRgba color) => ColorBrush = ToBrush(color);

    public RelayCommand PickColorCommand { get; }
    public RelayCommand RemoveCommand { get; }

    private static Brush ToBrush(ColorRgba c)
    {
        var brush = new SolidColorBrush(BitmapConverter.ToWpf(c));
        brush.Freeze();
        return brush;
    }
}

/// <summary>A ready-made set of chart colors shown as a small strip of squares.</summary>
public sealed class ChartPaletteItem
{
    public ChartPaletteItem(string name, IReadOnlyList<ColorRgba> colors)
    {
        Name = name;
        Colors = colors;
        Swatches = colors.Take(6).Select(c => { var b = new SolidColorBrush(BitmapConverter.ToWpf(c)); b.Freeze(); return (Brush)b; }).ToList();
    }

    public string Name { get; }
    public IReadOnlyList<ColorRgba> Colors { get; }
    public IReadOnlyList<Brush> Swatches { get; }
}

/// <summary>One of the chart types, as a button with a picture.</summary>
public sealed record ChartKindItem(ChartKind Kind, string Name, string IconKey, bool IsSelected);
