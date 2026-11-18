using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// The program's own message box: the same questions, warnings and errors as the Windows one (same buttons, same answers),
/// but dark like the rest of the program. Use <see cref="Show"/> where <c>MessageBox.Show</c> was used.
/// </summary>
public partial class MessageDialog : Window
{
    private MessageBoxResult _result = MessageBoxResult.None;
    private MessageBoxResult _escapeResult = MessageBoxResult.None;
    private readonly string? _yesText, _noText;

    private MessageDialog(string text, string caption, MessageBoxButton buttons, MessageBoxImage image, string? yesText, string? noText)
    {
        _yesText = yesText;
        _noText = noText;
        InitializeComponent();
        Title = caption;
        MessageText.Text = text;
        ShowIcon(image);
        AddButtons(buttons);
    }

    /// <summary>
    /// Shows the message and waits. Returns the button that was pressed (the X or Esc count as No / Cancel / OK, like Windows).
    /// <paramref name="yesText"/> / <paramref name="noText"/> rename the Yes / No buttons (e.g. Save / Don't save); the answer is still Yes / No.
    /// </summary>
    public static MessageBoxResult Show(Window? owner, string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None, string? yesText = null, string? noText = null)
    {
        var dialog = Create(text, caption, buttons, image, yesText, noText);
        if (owner != null && owner.IsVisible) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        return dialog._result == MessageBoxResult.None ? dialog._escapeResult : dialog._result;
    }

    /// <summary>The window, not shown yet (<see cref="Show"/> shows it).</summary>
    public static MessageDialog Create(string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None, string? yesText = null, string? noText = null)
        => new(text, caption, buttons, image, yesText, noText);

    private void ShowIcon(MessageBoxImage image)
    {
        (string sign, string color) = image switch
        {
            MessageBoxImage.Error => ("×", "#FFEF4444"),          // x on red
            MessageBoxImage.Warning => ("!", "#FFF59E0B"),             // ! on amber
            MessageBoxImage.Question => ("?", "#FF3B82F6"),            // ? on blue
            MessageBoxImage.Information => ("i", "#FF3B82F6"),         // i on blue
            _ => ("", "#00000000"),
        };
        if (sign.Length == 0)
        {
            IconDisc.Visibility = Visibility.Collapsed;
            return;
        }
        IconSign.Text = sign;
        IconDisc.Background = (Brush)new BrushConverter().ConvertFromString(color)!;
    }

    private void AddButtons(MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OK:
                Add("OK", MessageBoxResult.OK, primary: true);
                _escapeResult = MessageBoxResult.OK;
                break;
            case MessageBoxButton.OKCancel:
                Add("OK", MessageBoxResult.OK, primary: true);
                Add("Cancel", MessageBoxResult.Cancel);
                _escapeResult = MessageBoxResult.Cancel;
                break;
            case MessageBoxButton.YesNo:
                Add(_yesText ?? "Yes", MessageBoxResult.Yes, primary: true);
                Add(_noText ?? "No", MessageBoxResult.No);
                _escapeResult = MessageBoxResult.No;
                break;
            case MessageBoxButton.YesNoCancel:
                Add(_yesText ?? "Yes", MessageBoxResult.Yes, primary: true);
                Add(_noText ?? "No", MessageBoxResult.No);
                Add("Cancel", MessageBoxResult.Cancel);
                _escapeResult = MessageBoxResult.Cancel;
                break;
        }
    }

    private void Add(string text, MessageBoxResult result, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            IsDefault = primary,
            Style = (Style)FindResource(primary ? "DialogPrimaryButton" : "DialogButton"),
            Margin = new Thickness(ButtonRow.Children.Count == 0 ? 0 : 8, 0, 0, 0),
            MinWidth = 88,
        };
        button.Click += (_, _) =>
        {
            _result = result;
            Close();
        };
        ButtonRow.Children.Add(button);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();                                 // answers as Windows does: No / Cancel / OK
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            try { Clipboard.SetText(MessageText.Text); } catch (System.Runtime.InteropServices.ExternalException) { }
        }
    }
}
