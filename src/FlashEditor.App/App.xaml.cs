using System.Windows;
using System.Windows.Threading;
using FlashEditor.App.Services;
using FlashEditor.App.ViewModels;
using FlashEditor.App.Views;

namespace FlashEditor.App;

/// <summary>Application entry point: wires services, the main view model and the main window together.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        // Lets object layers (shapes, text, placed images) draw themselves.
        Core.Objects.ObjectRendering.Renderer = new Rendering.ObjectRenderer();

        var dialogs = new DialogService();
        var viewModel = new MainViewModel(dialogs, new ClipboardService());
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();

        // "Open with Flash Editor" / drag a file onto the .exe
        // (several files = one tab each)
        foreach (var path in e.Args)
            viewModel.File.OpenPath(path);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(MainWindow, $"Something went wrong:\n\n{e.Exception.Message}", "Flash Editor",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // keep the app (and the user's work) alive
    }
}
