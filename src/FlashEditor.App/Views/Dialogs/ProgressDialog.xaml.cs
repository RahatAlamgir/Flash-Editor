using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Progress bar with an estimate of the time left, and a Cancel button (closing the window cancels too).</summary>
public partial class ProgressDialog : Window
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _finished;

    public ProgressDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        DetailText.Text = "Starting...";
    }

    /// <summary>Raised when the user clicks Cancel or closes the window.</summary>
    public event Action? CancelRequested;

    /// <param name="fraction">0..1</param>
    /// <param name="detail">Optional extra text (e.g. "Layer 2 of 3").</param>
    public void Report(double fraction, string? detail = null)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        Bar.Value = fraction;
        PercentText.Text = $"{fraction:P0}";
        string left = "";
        if (fraction > 0.02)
        {
            var remaining = TimeSpan.FromSeconds(_clock.Elapsed.TotalSeconds * (1 - fraction) / fraction);
            left = remaining.TotalSeconds < 60 ? $"about {Math.Ceiling(remaining.TotalSeconds):0} s left" : $"about {Math.Ceiling(remaining.TotalMinutes):0} min left";
        }
        DetailText.Text = string.Join("  ·  ", new[] { detail, left }.Where(s => !string.IsNullOrEmpty(s)));
    }

    /// <summary>Closes the window when the job is done (not a cancel).</summary>
    public void Finish()
    {
        _finished = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        DetailText.Text = "Cancelling...";
        CancelRequested?.Invoke();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_finished)
        {
            // Closing with X = cancel; the window closes once the job has stopped.
            e.Cancel = true;
            OnCancel(this, new RoutedEventArgs());
        }
        base.OnClosing(e);
    }
}
