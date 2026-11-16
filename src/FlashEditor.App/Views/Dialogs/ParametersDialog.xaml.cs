using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FlashEditor.Core.Effects;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// Generic settings dialog built from <see cref="EffectParameter"/> descriptions (sliders and
/// checkboxes). Calls the preview callback shortly after the user stops moving a slider.
/// </summary>
public partial class ParametersDialog : Window
{
    private readonly List<ParameterRow> _rows;
    private readonly Action<EffectValues>? _onChanged;
    private readonly DispatcherTimer _debounce;

    public ParametersDialog(string title, IReadOnlyList<EffectParameter> parameters, EffectValues initial,
        Action<EffectValues>? onChanged, string? description)
    {
        InitializeComponent();
        Title = title;
        _onChanged = onChanged;

        if (!string.IsNullOrWhiteSpace(description))
        {
            DescriptionText.Text = description;
            DescriptionText.Visibility = Visibility.Visible;
        }

        _rows = parameters.Select(p => new ParameterRow(p, initial.Get(p.Key, p.Default))).ToList();
        foreach (var row in _rows) row.Changed += (_, _) => SchedulePreview();
        RowsList.ItemsSource = _rows;

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _onChanged?.Invoke(Values);
        };

        Loaded += (_, _) =>
        {
            _onChanged?.Invoke(Values); // show the preview with the starting values
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        };
    }

    /// <summary>The values currently set in the dialog.</summary>
    public EffectValues Values
    {
        get
        {
            var values = new EffectValues();
            foreach (var row in _rows) values[row.Parameter.Key] = row.Value;
            return values;
        }
    }

    private void SchedulePreview()
    {
        if (_onChanged == null) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.Value = row.Parameter.Default;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        // Commit a value still being typed in a text box.
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox box)
            box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        _debounce.Stop();
        DialogResult = true;
    }
}
