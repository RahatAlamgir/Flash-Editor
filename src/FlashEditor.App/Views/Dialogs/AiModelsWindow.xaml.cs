using System.Diagnostics;
using System.IO;
using System.Windows;
using FlashEditor.App.ViewModels;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Settings > AI Models: the AI model files on this PC, with buttons to download or delete each one.</summary>
public partial class AiModelsWindow : Window
{
    private readonly AiModelsViewModel _viewModel;

    public AiModelsWindow(AiModelsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closing += (_, _) => _viewModel.CancelAll();      // a download that is still running is stopped (and its half-finished file removed)
    }

    // Esc closes the window (the X in the corner does too).
    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) Close();
    }

    private void OnShowFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_viewModel.FolderPath);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_viewModel.FolderPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageDialog.Show(this, ex.Message, "Show folder", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
