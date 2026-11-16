using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FlashEditor.Core.IO;
using FlashEditor.Core.Transforms;

namespace FlashEditor.App.Views.Dialogs.Controls;

/// <summary>
/// Pixel size editor used by Image Size and Export: width, height and percent stay linked
/// (when "Keep proportions" is on), with quick buttons such as 50% or 200%.
/// </summary>
public partial class SizeEditor : UserControl
{
    private static readonly int[] PresetPercents = { 10, 25, 50, 75, 100, 150, 200, 300, 400 };
    private static readonly ResampleMode[] Modes =
        { ResampleMode.Lanczos, ResampleMode.Bicubic, ResampleMode.Smooth, ResampleMode.NearestNeighbor };

    private int _originalWidth = 1;
    private int _originalHeight = 1;
    private bool _updating;

    public SizeEditor()
    {
        InitializeComponent();
        foreach (int percent in PresetPercents)
        {
            var button = new Button { Content = $"{percent}%", Margin = new Thickness(0, 0, 6, 6), MinWidth = 52, Padding = new Thickness(6, 3, 6, 3) };
            button.Click += (_, _) => SetPercent(percent);
            PresetPanel.Children.Add(button);
        }
    }

    /// <summary>Raised whenever the size or resize method changes (also while typing).</summary>
    public event EventHandler? Changed;

    public ResampleMode Mode
    {
        get => Modes[Math.Max(0, ModeBox.SelectedIndex)];
        set => ModeBox.SelectedIndex = Math.Max(0, Array.IndexOf(Modes, value));
    }

    /// <summary>Sets the starting size (100%).</summary>
    public void Initialize(int width, int height)
    {
        _originalWidth = Math.Max(1, width);
        _originalHeight = Math.Max(1, height);
        SetPercent(100);
    }

    /// <summary>
    /// Changes the size that counts as 100% (e.g. after cropping) while keeping the chosen percentage.
    /// </summary>
    public void SetSourceSize(int width, int height)
    {
        double percent = double.TryParse(PercentBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double p) && p > 0 ? p : 100;
        _originalWidth = Math.Max(1, width);
        _originalHeight = Math.Max(1, height);
        SetPercent(percent);
    }

    public void FocusWidth()
    {
        WidthBox.Focus();
        WidthBox.SelectAll();
    }

    /// <summary>Reads the chosen size. Returns false with a message when the values are not valid.</summary>
    public bool TryGetSize(out int width, out int height, out string error)
    {
        error = string.Empty;
        bool ok = int.TryParse(WidthBox.Text, out width) & int.TryParse(HeightBox.Text, out height);
        if (!ok || width < 1 || height < 1 || width > ImageDecoder.MaxDimension || height > ImageDecoder.MaxDimension)
        {
            error = $"Width and height must be whole numbers between 1 and {ImageDecoder.MaxDimension} px.";
            return false;
        }
        return true;
    }

    private void SetPercent(double percent)
    {
        Update(() =>
        {
            WidthBox.Text = Scale(_originalWidth, percent).ToString();
            HeightBox.Text = Scale(_originalHeight, percent).ToString();
            PercentBox.Text = percent.ToString("0.#", CultureInfo.CurrentCulture);
        });
    }

    private static int Scale(int size, double percent) => Math.Max(1, (int)Math.Round(size * percent / 100));

    private void OnWidthChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating || !int.TryParse(WidthBox.Text, out int w) || w <= 0) return;
        Update(() =>
        {
            if (KeepRatioBox.IsChecked == true)
                HeightBox.Text = Math.Max(1, (int)Math.Round(w * (double)_originalHeight / _originalWidth)).ToString();
            PercentBox.Text = (w * 100.0 / _originalWidth).ToString("0.#", CultureInfo.CurrentCulture);
        });
    }

    private void OnHeightChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating || !int.TryParse(HeightBox.Text, out int h) || h <= 0) return;
        Update(() =>
        {
            if (KeepRatioBox.IsChecked == true)
                WidthBox.Text = Math.Max(1, (int)Math.Round(h * (double)_originalWidth / _originalHeight)).ToString();
            PercentBox.Text = (h * 100.0 / _originalHeight).ToString("0.#", CultureInfo.CurrentCulture);
        });
    }

    private void OnPercentChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating || !double.TryParse(PercentBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double p) || p <= 0) return;
        Update(() =>
        {
            WidthBox.Text = Scale(_originalWidth, p).ToString();
            HeightBox.Text = Scale(_originalHeight, p).ToString();
        });
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e) => RaiseChanged();

    private void Update(Action change)
    {
        _updating = true;
        try { change(); }
        finally { _updating = false; }
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        if (HintText == null) return; // during InitializeComponent
        if (TryGetSize(out int w, out int h, out _))
        {
            long before = (long)_originalWidth * _originalHeight, after = (long)w * h;
            HintText.Text = after > before
                ? $"Enlarging to {w} x {h} px ({after / 1e6:0.##} MP). Adds pixels; it cannot invent real detail, but Lanczos keeps edges crisp."
                : after < before
                    ? $"Shrinking to {w} x {h} px ({after / 1e6:0.##} MP). Smaller file and lower resolution."
                    : $"{w} x {h} px (unchanged).";
        }
        else
        {
            HintText.Text = string.Empty;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
