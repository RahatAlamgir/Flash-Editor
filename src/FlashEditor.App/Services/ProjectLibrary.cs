using System.IO;
using System.Text.Json;
using FlashEditor.App.Rendering;
using FlashEditor.Core.IO.Project;

namespace FlashEditor.App.Services;

/// <summary>
/// Knows which projects the user has: every .flash file in the projects folder (where new projects are created) plus the projects
/// opened or saved elsewhere (the "recent" list). The Project Manager shows what this lists.
/// </summary>
public sealed class ProjectLibrary
{
    private const int MaxRecent = 60;

    private sealed class Settings
    {
        public string? ProjectsFolder { get; set; }
        public List<string> Recent { get; set; } = new();
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string _settingsFile;
    private Settings _settings = new();

    /// <param name="settingsFolder">Where the list is kept (default: the user's own program data folder).</param>
    /// <param name="defaultProjectsFolder">Where new projects go unless the user chose another folder (default: Documents\Flash Editor Projects).</param>
    public ProjectLibrary(string? settingsFolder = null, string? defaultProjectsFolder = null)
    {
        settingsFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashEditor");
        _settingsFile = Path.Combine(settingsFolder, "projects.json");
        DefaultProjectsFolder = defaultProjectsFolder
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Flash Editor Projects");
        Load();
    }

    /// <summary>The library the program uses.</summary>
    public static ProjectLibrary Default { get; set; } = new();

    public string DefaultProjectsFolder { get; }

    /// <summary>The folder where new projects are created and looked for.</summary>
    public string ProjectsFolder
    {
        get => string.IsNullOrWhiteSpace(_settings.ProjectsFolder) ? DefaultProjectsFolder : _settings.ProjectsFolder!;
        set
        {
            _settings.ProjectsFolder = value;
            Save();
        }
    }

    public IReadOnlyList<string> Recent => _settings.Recent;

    /// <summary>Puts a project at the top of the recent list (called when one is opened or saved).</summary>
    public void AddRecent(string path)
    {
        path = Path.GetFullPath(path);
        _settings.Recent.RemoveAll(p => SamePath(p, path));
        _settings.Recent.Insert(0, path);
        if (_settings.Recent.Count > MaxRecent) _settings.Recent.RemoveRange(MaxRecent, _settings.Recent.Count - MaxRecent);
        Save();
    }

    /// <summary>Takes a project out of the recent list (the file stays where it is).</summary>
    public void Forget(string path)
    {
        if (_settings.Recent.RemoveAll(p => SamePath(p, path)) > 0) Save();
    }

    /// <summary>Every project file the library knows that still exists.</summary>
    public List<string> KnownPaths()
    {
        var paths = new List<string>();
        void Add(string path)
        {
            if (File.Exists(path) && !paths.Any(p => SamePath(p, path))) paths.Add(Path.GetFullPath(path));
        }
        foreach (var path in _settings.Recent) Add(path);
        try
        {
            if (Directory.Exists(ProjectsFolder))
                foreach (var file in Directory.EnumerateFiles(ProjectsFolder, "*" + ProjectFile.Extension)) Add(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* an unreachable folder lists nothing */ }
        return paths;
    }

    /// <summary>Reads the information and the preview of every known project (newest first). Meant to run in the background.</summary>
    public List<ProjectEntry> Load(CancellationToken cancel = default)
    {
        var entries = new List<ProjectEntry>();
        foreach (var path in KnownPaths())
        {
            cancel.ThrowIfCancellationRequested();
            entries.Add(ReadEntry(path));
        }
        return entries.OrderByDescending(e => e.Modified).ToList();
    }

    public static ProjectEntry ReadEntry(string path)
    {
        DateTime modified = DateTime.MinValue;
        long size = 0;
        try
        {
            var file = new FileInfo(path);
            modified = file.LastWriteTime;
            size = file.Length;
            var info = ProjectReader.ReadInfo(path);
            var thumbnail = info.Preview == null ? null : BitmapConverter.ToFrozenBitmap(info.Preview);
            return new ProjectEntry(path, modified, size, info.PageCount, info.Width, info.Height, thumbnail);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new ProjectEntry(path, modified, size, 0, 0, 0, null, "Cannot be read");
        }
    }

    /// <summary>A file name for a new project in the projects folder: the name, made safe, with (2), (3)... when it is taken.</summary>
    public string NewProjectPath(string name)
    {
        name = CleanName(name);
        string folder = ProjectsFolder;
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name + ProjectFile.Extension);
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(folder, $"{name} ({i}){ProjectFile.Extension}");
        return path;
    }

    /// <summary>Removes characters a file name cannot have; an empty result becomes "Untitled project".</summary>
    public static string CleanName(string? name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string((name ?? "").Where(c => !bad.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return clean.Length == 0 ? "Untitled project" : clean.Length > 80 ? clean[..80].TrimEnd() : clean;
    }

    private static bool SamePath(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
                _settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsFile), Json) ?? new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { _settings = new Settings(); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllText(_settingsFile, JsonSerializer.Serialize(_settings, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* the list is a convenience; never disturb the user */ }
    }
}
