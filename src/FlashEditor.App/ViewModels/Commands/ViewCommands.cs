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
        ShortcutsCommand = new RelayCommand(() => _main.Dialogs.ShowInfo("Keyboard Shortcuts", ShortcutsText));
        AboutCommand = new RelayCommand(() => _main.Dialogs.ShowInfo("About Flash Editor",
            "Flash Editor 1.0\nA small layer-based image editor built with C#, WPF and SkiaSharp."));
    }

    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand FitCommand { get; }
    public RelayCommand ActualSizeCommand { get; }
    public RelayCommand ShortcutsCommand { get; }
    public RelayCommand AboutCommand { get; }

    public const string ShortcutsText =
        "TOOLS\n" +
        "V Move   M Rectangle select   L Lasso   C Crop\n" +
        "B Brush   E Eraser   G Paint bucket   I Eyedropper\n" +
        "U Rectangle   O Ellipse   N Line   T Text   H Hand   Z Zoom\n" +
        "[ / ]  Brush size smaller / bigger\n" +
        "X Swap colors   D Default colors (black / white)\n\n" +
        "EDIT\n" +
        "Ctrl+Z Undo   Ctrl+Y or Ctrl+Shift+Z Redo\n" +
        "Ctrl+C Copy   Ctrl+X Cut   Ctrl+V Paste   Delete Clear selection\n" +
        "Ctrl+A Select all layers (Select / Move tool) or all pixels (other tools)   Ctrl+Shift+D Deselect   Ctrl+Shift+I Invert selection\n" +
        "Ctrl+D Duplicate the selected layer or object\n\n" +
        "FILE\n" +
        "Ctrl+N New   Ctrl+O Open   Ctrl+S Save   Ctrl+Shift+S Save As\n" +
        "Ctrl+Alt+S Export (choose quality and size)\n\n" +
        "SELECT / MOVE (V)\n" +
        "Click to select   Drag to move   Handles resize / rotate (Shift = free / 15°)\n" +
        "Double-click text to edit   Right-click for quick actions   Delete removes a selected object\n" +
        "Ctrl+] / Ctrl+[ forward / backward   Ctrl+Shift+] / [ front / back\n\n" +
        "REFINE BRUSH (R)\n" +
        "Restore = paint back what Remove Background took   Remove = paint away leftovers   Alt = other mode   X = switch\n\n" +
        "SELECTIONS\n" +
        "M rectangle / ellipse   L lasso / polygon lasso   P pen (curves)   W magic wand   Shift = add, Alt = subtract\n\n" +
        "IMAGE\n" +
        "Ctrl+Alt+I Image size / resolution   Ctrl+Alt+C Canvas size   Ctrl+Alt+B Remove background\n\n" +
        "VIEW\n" +
        "Mouse wheel Zoom   Space+drag or middle mouse Pan\n" +
        "Ctrl+Plus Zoom in   Ctrl+Minus Zoom out   Ctrl+0 Fit   Ctrl+1 100%\n\n" +
        "CROP TOOL\n" +
        "Enter Apply   Esc Cancel";
}
