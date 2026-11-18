using System.Windows;
using FlashEditor.App.Services;
using FlashEditor.Core.History;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Settings > Undo Steps: choices that are kept between runs (how many undo steps a project keeps).</summary>
public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
        StepsSlider.Value = AppSettings.Current.UndoSteps;
    }

    private void OnDefault(object sender, RoutedEventArgs e) => StepsSlider.Value = ProjectHistory.DefaultSteps;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.UndoSteps = (int)Math.Round(StepsSlider.Value);
        AppSettings.Current.Apply();
        DialogResult = true;
    }
}
