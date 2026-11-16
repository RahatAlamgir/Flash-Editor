using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlashEditor.App.Views.Controls;

/// <summary>
/// Slider with a number box next to it. Drag for quick changes, or type an exact value and press
/// Enter. Up/Down arrows in the box change the value by one step. Values are clamped to the range.
/// </summary>
public partial class NumberSlider : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumberSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NumberSlider)d).ShowValue()));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumberSlider), new PropertyMetadata(0.0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumberSlider), new PropertyMetadata(100.0));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(NumberSlider), new PropertyMetadata(1.0));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NumberSlider), new PropertyMetadata(""));
    public static readonly DependencyProperty SliderWidthProperty = DependencyProperty.Register(nameof(SliderWidth), typeof(double), typeof(NumberSlider), new PropertyMetadata(100.0));
    public static readonly DependencyProperty BoxWidthProperty = DependencyProperty.Register(nameof(BoxWidth), typeof(double), typeof(NumberSlider), new PropertyMetadata(58.0));
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(NumberSlider), new PropertyMetadata(0, (d, _) => ((NumberSlider)d).ShowValue()));

    private bool _editing;

    public NumberSlider()
    {
        InitializeComponent();
        ShowValue();
    }

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double SliderWidth { get => (double)GetValue(SliderWidthProperty); set => SetValue(SliderWidthProperty, value); }
    public double BoxWidth { get => (double)GetValue(BoxWidthProperty); set => SetValue(BoxWidthProperty, value); }

    /// <summary>Digits after the decimal point shown in the box.</summary>
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    private void ShowValue()
    {
        if (Box == null || _editing) return;
        Box.Text = Math.Round(Value, Decimals).ToString("0." + new string('#', Math.Max(0, Decimals)), CultureInfo.CurrentCulture).TrimEnd('.');
    }

    private void Commit()
    {
        if (double.TryParse(Box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double v)
            || double.TryParse(Box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
        {
            Value = Math.Clamp(v, Minimum, Maximum);
        }
        _editing = false;
        ShowValue(); // shows the clamped value, or the old one after a typo
    }

    private void OnBoxGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _editing = true;
        Box.SelectAll();
    }

    private void OnBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit();

    private void OnBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit();
            _editing = true;
            Box.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _editing = false;
            ShowValue();
            e.Handled = true;
        }
    }

    private void OnBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down)) return;
        Value = Math.Clamp(Value + (e.Key == Key.Up ? Step : -Step), Minimum, Maximum);
        _editing = false;
        ShowValue();
        _editing = true;
        Box.SelectAll();
        e.Handled = true;
    }
}
