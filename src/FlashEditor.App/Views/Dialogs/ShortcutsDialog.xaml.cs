using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.Services;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Settings > Keyboard Shortcuts: every shortcut in a list. Click one, press the new keys.</summary>
public partial class ShortcutsDialog : Window
{
    private const string DefaultHint = "Click a shortcut, then press the new keys. Backspace removes it, Esc cancels.";

    private readonly Dictionary<ShortcutEntry, (Button Chip, Button Reset)> _rows = new();
    private ShortcutEntry? _capturing;

    public ShortcutsDialog()
    {
        InitializeComponent();
        string? section = null;
        foreach (var entry in Shortcuts.Entries)
        {
            if (entry.Section != section)
            {
                section = entry.Section;
                List.Children.Add(new TextBlock
                {
                    Text = section.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 4),
                    Foreground = (Brush)FindResource("SubtleTextBrush"),
                });
            }
            List.Children.Add(MakeRow(entry));
        }
        PreviewKeyDown += OnKey;
        Deactivated += (_, _) => StopCapture();
        Closed += (_, _) => StopCapture();
    }

    private UIElement MakeRow(ShortcutEntry entry)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        grid.Children.Add(new TextBlock { Text = entry.Title, VerticalAlignment = VerticalAlignment.Center });

        var chip = new Button
        {
            MinWidth = 150, Padding = new Thickness(10, 3, 10, 3), HorizontalContentAlignment = HorizontalAlignment.Center,
            ToolTip = "Click, then press the new keys",
        };
        chip.Click += (_, _) => StartCapture(entry);
        Grid.SetColumn(chip, 1);
        grid.Children.Add(chip);

        var reset = new Button
        {
            Content = "↺", Width = 28, Padding = new Thickness(0, 2, 0, 2), Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Back to the normal shortcut", Background = Brushes.Transparent, BorderThickness = new Thickness(0),
        };
        reset.Click += (_, _) => { Shortcuts.Reset(entry); StopCapture(); Refresh(); };
        Grid.SetColumn(reset, 2);
        grid.Children.Add(reset);

        _rows[entry] = (chip, reset);
        Refresh(entry);
        return grid;
    }

    private void Refresh()
    {
        foreach (var entry in _rows.Keys) Refresh(entry);
    }

    private void Refresh(ShortcutEntry entry)
    {
        var (chip, reset) = _rows[entry];
        if (ReferenceEquals(entry, _capturing)) { chip.Content = "Press the new keys..."; chip.Foreground = (Brush)FindResource("AccentBrush"); }
        else
        {
            string text = Shortcuts.Pretty(Shortcuts.Current(entry));
            chip.Content = text.Length == 0 ? "(none)" : text;
            chip.Foreground = text.Length == 0 ? (Brush)FindResource("SubtleTextBrush") : (Brush)FindResource("TextBrush");
        }
        reset.Visibility = Shortcuts.IsChanged(entry) ? Visibility.Visible : Visibility.Hidden;
    }

    private void StartCapture(ShortcutEntry entry)
    {
        var before = _capturing;
        _capturing = entry;
        Hint.Text = entry.IsTool ? "Press one key for this tool. Backspace removes the shortcut, Esc cancels." : "Press Ctrl or Alt with a key (or an F key). Backspace removes the shortcut, Esc cancels.";
        if (before != null) Refresh(before);
        Refresh(entry);
        Focus();
    }

    private void StopCapture()
    {
        if (_capturing == null) return;
        var was = _capturing;
        _capturing = null;
        if (_rows.ContainsKey(was)) Refresh(was);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_capturing == null)
        {
            if (key == Key.Escape) { Close(); e.Handled = true; }
            return;
        }
        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None or Key.DeadCharProcessed) return;
        var entry = _capturing;
        if (key == Key.Escape) { StopCapture(); Hint.Text = DefaultHint; return; }

        string? problem = null;
        string? text = key == Key.Back ? "" : Shortcuts.FromKeys(entry, key, Keyboard.Modifiers, out problem);
        if (text == null)
        {
            Hint.Text = problem ?? DefaultHint;
            return;
        }
        _capturing = null;
        string? taken = Shortcuts.Set(entry, text);
        Hint.Text = taken != null ? $"\"{taken}\" had this shortcut and has none now." : DefaultHint;
        Refresh();
    }

    private void OnResetAll(object sender, RoutedEventArgs e)
    {
        _capturing = null;
        Shortcuts.ResetAll();
        Hint.Text = DefaultHint;
        Refresh();
    }
}
