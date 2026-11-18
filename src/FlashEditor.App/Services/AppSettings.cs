using System.IO;
using System.Text.Json;
using FlashEditor.Core.History;

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

    /// <summary>Raised after <see cref="Apply"/> changed something.</summary>
    public static event EventHandler? Changed;

    /// <summary>Saves the settings and tells the program (open projects change their number of undo steps at once).</summary>
    public void Apply()
    {
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath));
                if (data != null) return new AppSettings { UndoSteps = data.UndoSteps };
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Stored(UndoSteps), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { /* the folder cannot be written: the choice only lasts until the program is closed */ }
    }

    private sealed record Stored(int UndoSteps);
}
