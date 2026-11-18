using FlashEditor.App.ViewModels.Infrastructure;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>View and Help menus.</summary>
public sealed class ViewCommands
{
    private readonly MainViewModel _main;

    public ViewCommands(MainViewModel main)
    {
        _main = main;
        ZoomInCommand = new RelayCommand(() => _main.Canvas?.ZoomIn());
        ZoomOutCommand = new RelayCommand(() => _main.Canvas?.ZoomOut());
        FitCommand = new RelayCommand(() => _main.Canvas?.FitToWindow());
        ActualSizeCommand = new RelayCommand(() => _main.Canvas?.ActualSize());
        ToggleRulersCommand = new RelayCommand(() => _main.Aids.ShowRulers = !_main.Aids.ShowRulers);
        ToggleGridCommand = new RelayCommand(() => _main.Aids.ShowGrid = !_main.Aids.ShowGrid);
        ToggleGuidesCommand = new RelayCommand(() => _main.Aids.ShowGuides = !_main.Aids.ShowGuides);
        ToggleSnapCommand = new RelayCommand(() => _main.Aids.SnapEnabled = !_main.Aids.SnapEnabled);
        ClearGuidesCommand = new RelayCommand(() => _main.Document.ClearGuides());
        SettingsCommand = new RelayCommand(() => _main.Dialogs.ShowSettings());
        BrandKitCommand = new RelayCommand(() => _main.Dialogs.ShowBrandKit(_main.Document.Model.Layers.Count == 0 ? null : Core.Painting.Compositor.Flatten(_main.Document.Model)));
        TourCommand = new RelayCommand(ShowTour);
        ShortcutsCommand = new RelayCommand(() => _main.Dialogs.ShowShortcuts());
        AboutCommand = new RelayCommand(() => _main.Dialogs.ShowInfo("About Flash Editor",
            "Flash Editor 1.0\nA small layer-based image editor built with C#, WPF and SkiaSharp."));
    }

    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand FitCommand { get; }
    public RelayCommand ActualSizeCommand { get; }
    public RelayCommand ToggleRulersCommand { get; }
    public RelayCommand ToggleGridCommand { get; }
    public RelayCommand ToggleGuidesCommand { get; }
    public RelayCommand ToggleSnapCommand { get; }
    public RelayCommand ClearGuidesCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand BrandKitCommand { get; }
    public RelayCommand ShortcutsCommand { get; }
    public RelayCommand TourCommand { get; }

    public const string TourText =
        "A few things worth knowing:\n\n" +
        "•  Everything is a layer. Click an object with the Select / Move tool (V), then change it in Properties: size, rotation, colors, text designs, shadows and more.\n\n" +
        "•  Right-click a layer on the canvas or in the Layers panel for quick options. Drag layers up and down to reorder them.\n\n" +
        "•  Crop (C) and Perspective Crop work like a selection: drag a box, adjust it, press Enter. The original layer is kept, hidden.\n\n" +
        "•  Ctrl+Z undoes anything, in every page of a project. The History panel lists the steps.\n\n" +
        "•  AI tools (Remove Background, Click Select, Remove / Fill...) ask once before downloading their model. Settings > AI Models shows them.\n\n" +
        "•  Settings > Brand Kit keeps your colors and fonts. File > Save as Template keeps a design for new pictures.\n\n" +
        "Press F1 any time for the keyboard shortcuts. This tour is in the Help menu.";

    /// <summary>Help > Quick Tour (also shown once, the first time the program is used).</summary>
    private void ShowTour()
    {
        _main.Dialogs.ShowInfo("Welcome to Flash Editor", TourText);
        Services.AppSettings.Current.MarkTourSeen();
    }

    /// <summary>Shows the tour the very first time only.</summary>
    public void ShowTourOnFirstRun()
    {
        if (!Services.AppSettings.Current.TourSeen) ShowTour();
    }
    public RelayCommand AboutCommand { get; }

}
