namespace FlashEditor.Core.IO.Project;

/// <summary>Names and numbers of the Flash Editor project file (a zip with project.json and one PNG per picture).</summary>
public static class ProjectFile
{
    /// <summary>File extension of a saved project.</summary>
    public const string Extension = ".flash";

    /// <summary>Dialog filter for project files.</summary>
    public const string Filter = "Flash Editor project|*.flash";

    /// <summary>Written into every file; a newer number than this one cannot be opened.</summary>
    public const int CurrentVersion = 1;

    public const string FormatName = "flash-project";
    internal const string ManifestEntry = "project.json";
    internal const string ImageFolder = "images/";

    public static bool IsProject(string path) => string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
}
