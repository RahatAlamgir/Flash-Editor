namespace FlashEditor.Core.IO.Project;

/// <summary>Names and numbers of the Flash Editor project file (a zip with project.json and one PNG per picture).</summary>
public static class ProjectFile
{
    /// <summary>File extension of a saved project.</summary>
    public const string Extension = ".flash";

    /// <summary>Dialog filter for project files.</summary>
    public const string Filter = "Flash Editor project|*.flash";

    /// <summary>Written into every file; a newer number than this one cannot be opened.</summary>
    public const int CurrentVersion = 2;

    public const string FormatName = "flash-project";
    internal const string ManifestEntry = "project.json";
    internal const string ImageFolder = "images/";

    /// <summary>A small picture of the project, used by the Project Manager (so it need not open the whole file).</summary>
    internal const string PreviewEntry = "preview.png";

    /// <summary>Longest side of the preview, in pixels.</summary>
    public const int PreviewSize = 480;

    public static bool IsProject(string path) => string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
}
