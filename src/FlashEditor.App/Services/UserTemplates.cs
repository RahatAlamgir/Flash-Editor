using System.IO;
using FlashEditor.Core.Documents;
using FlashEditor.Core.IO.Project;

namespace FlashEditor.App.Services;

/// <summary>
/// Pictures the user saved as templates (File > Save as Template). Each is a normal project file in the "settings\templates" folder next
/// to FlashEditor.exe, so it keeps everything editable. In the New dialog they appear after the built-in layouts, marked with a star.
/// </summary>
public static class UserTemplates
{
    /// <summary>What a template's name starts with in the lists (and in <see cref="NewImageSettings.Template"/>).</summary>
    public const string Prefix = "★ ";

    private static string Folder => Path.Combine(AppContext.BaseDirectory, "settings", "templates");

    private static string PathOf(string name) => Path.Combine(Folder, SafeName(name) + ProjectFile.Extension);

    /// <summary>Letters, digits, spaces and a few marks only: the name becomes a file name.</summary>
    public static string SafeName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        string cleaned = new string(name.Trim().Select(c => bad.Contains(c) ? '-' : c).ToArray()).Trim(' ', '.');
        return cleaned.Length == 0 ? "Template" : cleaned.Length > 60 ? cleaned[..60] : cleaned;
    }

    public static bool IsUserTemplate(string? template) => template != null && template.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>The template names (without the star), A to Z.</summary>
    public static IReadOnlyList<string> Names()
    {
        try
        {
            if (!Directory.Exists(Folder)) return Array.Empty<string>();
            return Directory.EnumerateFiles(Folder, "*" + ProjectFile.Extension)
                .Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    public static bool Exists(string name) => File.Exists(PathOf(name));

    /// <summary>Saves the picture as a template (replaces one with the same name).</summary>
    public static void Save(Document document, string name)
    {
        Directory.CreateDirectory(Folder);
        ProjectWriter.Save(document, PathOf(name));
    }

    /// <summary>A fresh copy of the template to start a new picture from.</summary>
    public static Document Load(string name) => ProjectReader.Open(PathOf(name));

    public static void Delete(string name)
    {
        try { File.Delete(PathOf(name)); }
        catch (IOException) { /* in use or gone: nothing to do */ }
    }
}
