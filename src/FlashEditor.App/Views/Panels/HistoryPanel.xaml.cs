using System.Windows.Controls;

namespace FlashEditor.App.Views.Panels;

/// <summary>List of undo steps. Clicking a row jumps back (or forward) to that state.</summary>
public partial class HistoryPanel : UserControl
{
    public HistoryPanel() => InitializeComponent();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem != null) HistoryList.ScrollIntoView(HistoryList.SelectedItem);
    }
}
