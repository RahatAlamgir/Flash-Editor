using System.Windows.Controls;
using System.Windows.Input;

namespace FlashEditor.App.Views.Panels;

/// <summary>Properties of the selected object. Enter in a number box applies it right away.</summary>
public partial class PropertiesPanel : UserControl
{
    public PropertiesPanel()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter in a single-line box commits the value (multi-line text keeps Enter for new lines).
        // (Number boxes next to sliders handle Enter themselves; they have no binding here.)
        if (e.Key == Key.Enter && e.OriginalSource is TextBox { AcceptsReturn: false } box
            && box.GetBindingExpression(TextBox.TextProperty) is { } binding)
        {
            binding.UpdateSource();
            box.SelectAll();
            e.Handled = true;
        }
    }
}
