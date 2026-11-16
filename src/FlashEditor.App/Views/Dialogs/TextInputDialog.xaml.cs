using System.Windows;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Asks for a (multi-line) piece of text.</summary>
public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string prompt, string initialText)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initialText;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public string Text => InputBox.Text.Replace("\r\n", "\n");

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
