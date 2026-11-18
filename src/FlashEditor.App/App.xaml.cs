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

        // Avoid long "stop everything" cleanups while the user drags things around.
        System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;

        // Lets the program open and save more image formats through Windows' own codecs (TIFF, HEIC, RAW, GIF...).
        Services.WicImageCodec.Register();

        // Lets object layers (shapes, text, placed images) draw themselves.
        Core.Objects.ObjectRendering.Renderer = new Rendering.ObjectRenderer();

        var dialogs = new DialogService();
        var viewModel = new MainViewModel(dialogs, new ClipboardService());
        var window = new MainWindow(viewModel);
        MainWindow = window;

        // The Project Manager comes first (unless the program was started with a file to open).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if (e.Args.Length == 0 && !viewModel.File.ShowProjectManager(atStartup: true))
        {
            Shutdown();
            return;
        }
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();

        // Backs up unsaved work every minute; brings it back if the last session did not end normally.
        var autoSave = new AutoSaveService(viewModel);
        autoSave.OfferRestore(dialogs);
        autoSave.Start();
        window.Closed += (_, _) => autoSave.Stop();

        // "Open with Flash Editor" / drag a file onto the .exe
        // (several files = one tab each)
        foreach (var path in e.Args)
            viewModel.File.OpenPath(path);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string text = $"Something went wrong:\n\n{e.Exception.Message}";
        // Before the editor window is shown (the Project Manager at start-up) there is no window to belong to.
        var front = Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible) ?? MainWindow;   // the window the user is in (a dialog may be open)
        Views.Dialogs.MessageDialog.Show(front, text, "Flash Editor", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // keep the app (and the user's work) alive
    }
}
