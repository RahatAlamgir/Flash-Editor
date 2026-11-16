using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Effects;
using FlashEditor.Core.Effects.Adjustments;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// Color graph for <see cref="RecolorEffect"/>: choose the new color, see the result live on the canvas.
/// </summary>
public partial class RecolorDialog : Window
{
    private static readonly string[] QuickColors =
    {
        "#C62828", "#AD1457", "#6A1B9A", "#283593", "#1565C0", "#00838F", "#2E7D32", "#9E9D24",
        "#F9A825", "#EF6C00", "#5D4037", "#212121", "#757575", "#ECEFF1", "#F8BBD0", "#B3E5FC",
    };

    private readonly ColorRgba _original;
    private readonly EffectValues _values;
    private readonly Action<EffectValues>? _onChanged;
    private readonly DispatcherTimer _debounce;
    private bool _updatingHex;
    private bool _initializing = true;

    public RecolorDialog(ColorRgba original, EffectValues values, Action<EffectValues>? onChanged)
    {
        InitializeComponent();
        _original = original;
        _values = values;
        _onChanged = onChanged;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _onChanged?.Invoke(_values.Clone()); };

        BeforeSwatch.Fill = new SolidColorBrush(BitmapConverter.ToWpf(original));
        SwatchList.ItemsSource = QuickColors.Select(h => new SolidColorBrush((Color)ColorConverter.ConvertFromString(h))).ToList();

        Graph.ColorChanged += (_, _) => OnColorChanged();
        DependencyPropertyDescriptor.FromProperty(FlashEditor.App.Views.Controls.NumberSlider.ValueProperty, typeof(FlashEditor.App.Views.Controls.NumberSlider))
            .AddValueChanged(ShadingSlider, (_, _) => { _values["shading"] = ShadingSlider.Value; Schedule(); });
        DependencyPropertyDescriptor.FromProperty(FlashEditor.App.Views.Controls.NumberSlider.ValueProperty, typeof(FlashEditor.App.Views.Controls.NumberSlider))
            .AddValueChanged(StrengthSlider, (_, _) => { _values["strength"] = StrengthSlider.Value; Schedule(); });

        Graph.Color = original; // starts as "no change" (no preview until the user picks)
        _initializing = false;
    }

    public EffectValues? Result { get; private set; }

    private void OnColorChanged()
    {
        var color = Graph.Color;
        RecolorEffect.SetTarget(_values, color);
        AfterSwatch.Fill = new SolidColorBrush(BitmapConverter.ToWpf(color));
        _updatingHex = true;
        HexBox.Text = color.ToHex();
        _updatingHex = false;
        Schedule();
    }

    private void Schedule()
    {
        if (_initializing) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnSwatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SolidColorBrush brush })
            Graph.Color = BitmapConverter.FromWpf(brush.Color);
    }

    private void OnHexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnHexCommitted(sender, e); e.Handled = true; }
    }

    private void OnHexCommitted(object sender, RoutedEventArgs e)
    {
        if (_updatingHex) return;
        if (ColorRgba.TryParseHex(HexBox.Text, out var color)) Graph.Color = color with { A = 255 };
        else HexBox.Text = Graph.Color.ToHex();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        ShadingSlider.Value = 100;
        StrengthSlider.Value = 100;
        Graph.Color = _original;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        _debounce.Stop();
        Result = _values.Clone();
        DialogResult = true;
    }
}
