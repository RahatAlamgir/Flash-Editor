using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Services;

/// <summary>The looks the user saved (Properties > Styles). They are kept in the user's own folder and are there in every project.</summary>
public sealed class StyleLibrary
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _file;

    public StyleLibrary(string? folder = null)
    {
        folder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashEditor");
        _file = Path.Combine(folder, "styles.json");
        Load();
    }

    /// <summary>The library the program uses.</summary>
    public static StyleLibrary Default { get; set; } = new();

    public ObservableCollection<SavedStyle> Items { get; } = new();

    /// <summary>Saves a look. A name that already exists is replaced.</summary>
    public void Add(string name, ObjectStyle style)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        var existing = Items.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null) Items[Items.IndexOf(existing)] = new SavedStyle(name, style);
        else Items.Add(new SavedStyle(name, style));
        Save();
    }

    public void Remove(SavedStyle style)
    {
        if (Items.Remove(style)) Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var items = JsonSerializer.Deserialize<List<SavedStyle>>(File.ReadAllText(_file), Json);
            if (items != null) foreach (var item in items) Items.Add(item);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { /* a damaged list starts empty */ }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(Items.ToList(), Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* saving the list is a convenience; never disturb the user */ }
    }
}
