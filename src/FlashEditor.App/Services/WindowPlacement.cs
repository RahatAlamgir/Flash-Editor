using System.IO;
using System.Text.Json;
using System.Windows;

namespace FlashEditor.App.Services;

/// <summary>Where the main window was when the program was closed.</summary>
public sealed record WindowSettings(double Left, double Top, double Width, double Height, bool IsMaximized);

/// <summary>
/// Remembers the main window's position, size and maximized state between runs.
/// The first time (nothing saved) the window opens maximized. The file lives in the "settings" folder next
/// to FlashEditor.exe (like the AI models), never in AppData. If that folder cannot be written the window
/// simply opens maximized every time.
/// </summary>
public static class WindowPlacement
{
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings", "window.json");

    /// <summary>Sets the window's size / position / state from the saved settings (call before Show).</summary>
    public static void Restore(Window window)
    {
        var saved = Load();
        if (saved == null)
        {
            window.WindowState = WindowState.Maximized;
            return;
        }

        if (IsUsable(saved, window))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = saved.Left;
            window.Top = saved.Top;
            window.Width = saved.Width;
            window.Height = saved.Height;
        }
        window.WindowState = saved.IsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    /// <summary>Saves the window's current place (call when it closes).</summary>
    public static void Save(Window window)
    {
        bool normal = window.WindowState == WindowState.Normal;
        var bounds = normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        if (bounds.IsEmpty || double.IsNaN(bounds.Left)) return;

        var settings = new WindowSettings(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            window.WindowState == WindowState.Maximized);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch (Exception)
        {
            // Not writable (for example a locked-down install folder): not worth bothering the user.
        }
    }

    private static WindowSettings? Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(FilePath)) : null;
        }
        catch (Exception)
        {
            return null; // damaged file: behave like the first run
        }
    }

    /// <summary>False when the saved place is nonsense or on a monitor that is no longer connected.</summary>
    private static bool IsUsable(WindowSettings s, Window window)
    {
        if (!double.IsFinite(s.Left) || !double.IsFinite(s.Top) || !double.IsFinite(s.Width) || !double.IsFinite(s.Height))
            return false;
        if (s.Width < window.MinWidth || s.Height < window.MinHeight) return false;

        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var title = new Rect(s.Left, s.Top, s.Width, Math.Min(s.Height, 60)); // the title bar must be reachable
        var overlap = Rect.Intersect(screen, title);
        return !overlap.IsEmpty && overlap.Width >= 100 && overlap.Height >= 30;
    }
}
