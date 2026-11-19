using System.IO;
using System.Text.Json;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Services;

/// <summary>
/// Choices the user can make in Settings. Saved in the "settings" folder next to FlashEditor.exe (like the window position); when that cannot
/// be read or written the defaults are used.
/// </summary>
public sealed class AppSettings
{
    private static AppSettings? _current;
    private int _undoSteps = ProjectHistory.DefaultSteps;

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings", "app.json");

    /// <summary>The settings in use.</summary>
    public static AppSettings Current => _current ??= Load();

    /// <summary>
    /// How many steps Undo can take back in a project (all its pages share them). Default 30, at most 60: every step keeps copies of pixels in
    /// memory, so more steps need more memory.
    /// </summary>
    public int UndoSteps
    {
        get => _undoSteps;
        set => _undoSteps = ProjectHistory.ClampSteps(value);
    }

    private const int MaxRecent = 14, MaxSwatches = 28;

    /// <summary>The colors picked last (newest first), as "#RRGGBB". Shown in the color picker.</summary>
    public List<string> RecentColors { get; private set; } = new();

    /// <summary>Colors the user saved (oldest first), as "#RRGGBB".</summary>
    public List<string> Swatches { get; private set; } = new();

    public const int MaxBrandColors = 12, MaxBrandFonts = 3;

    /// <summary>Shortcuts the user changed (Settings > Keyboard Shortcuts): id -> new shortcut, "" = none. Missing = the default.</summary>
    public Dictionary<string, string> Shortcuts { get; private set; } = new();

    /// <summary>Remembers a changed shortcut (null = back to the default).</summary>
    public void SetShortcut(string id, string? text)
    {
        if (text == null) Shortcuts.Remove(id); else Shortcuts[id] = text;
        Save();
    }

    public void ClearShortcuts()
    {
        Shortcuts.Clear();
        Save();
    }

    /// <summary>True once the tutorial project was made (it is made and opened the first time the program starts).</summary>
    public bool DemoCreated { get; set; }

    /// <summary>The look of the icons: Plain (gray) or Colorful.</summary>
    public string IconStyle { get; set; } = "Plain";

    /// <summary>True once the quick tour was shown (it opens by itself only the first time).</summary>
    public bool TourSeen { get; set; }

    /// <summary>The brand colors (Settings > Brand Kit), as "#RRGGBB". Shown first in every color picker.</summary>
    public List<string> BrandColors { get; private set; } = new();

    /// <summary>The brand fonts (at most 3). Shown as quick buttons when a text is selected.</summary>
    public List<string> BrandFonts { get; private set; } = new();

    /// <summary>Raised when the brand kit was saved.</summary>
    public static event EventHandler? BrandChanged;

    public void SetBrand(IEnumerable<string> colors, IEnumerable<string> fonts)
    {
        BrandColors = colors.Take(MaxBrandColors).ToList();
        BrandFonts = fonts.Take(MaxBrandFonts).ToList();
        Save();
        BrandChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string ToHex(ColorRgba c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Remembers a color that was just picked (the same color moves to the front).</summary>
    public void AddRecentColor(ColorRgba color)
    {
        string hex = ToHex(color);
        RecentColors.RemoveAll(h => h.Equals(hex, StringComparison.OrdinalIgnoreCase));
        RecentColors.Insert(0, hex);
        if (RecentColors.Count > MaxRecent) RecentColors.RemoveRange(MaxRecent, RecentColors.Count - MaxRecent);
        Save();
    }

    /// <summary>Saves a color as a swatch. Returns false when it is there already or there is no room.</summary>
    public bool AddSwatch(ColorRgba color)
    {
        string hex = ToHex(color);
        if (Swatches.Count >= MaxSwatches || Swatches.Any(h => h.Equals(hex, StringComparison.OrdinalIgnoreCase))) return false;
        Swatches.Add(hex);
        Save();
        return true;
    }

    public void RemoveSwatch(string hex)
    {
        if (Swatches.RemoveAll(h => h.Equals(hex, StringComparison.OrdinalIgnoreCase)) > 0) Save();
    }

    /// <summary>Raised after <see cref="Apply"/> changed something.</summary>
    public static event EventHandler? Changed;

    /// <summary>Saves the settings and tells the program (open projects change their number of undo steps at once).</summary>
    public void Apply()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Remembers that the quick tour was shown.</summary>
    public void MarkTourSeen()
    {
        TourSeen = true;
        Save();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath));
                if (data != null) return new AppSettings { UndoSteps = data.UndoSteps, RecentColors = data.RecentColors ?? new(), Swatches = data.Swatches ?? new(), BrandColors = data.BrandColors ?? new(), BrandFonts = data.BrandFonts ?? new(), TourSeen = data.TourSeen, Shortcuts = data.Shortcuts ?? new(), IconStyle = data.IconStyle == "Colorful" ? "Colorful" : "Plain", DemoCreated = data.DemoCreated };
            }
        }
        catch (Exception) { /* a damaged file: use the defaults */ }
        return new AppSettings();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Stored(UndoSteps, RecentColors, Swatches, BrandColors, BrandFonts, TourSeen, Shortcuts, IconStyle, DemoCreated), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { /* the folder cannot be written: the choice only lasts until the program is closed */ }
    }

    private sealed record Stored(int UndoSteps, List<string>? RecentColors = null, List<string>? Swatches = null, List<string>? BrandColors = null, List<string>? BrandFonts = null, bool TourSeen = false, Dictionary<string, string>? Shortcuts = null, string? IconStyle = null, bool DemoCreated = false);
}
