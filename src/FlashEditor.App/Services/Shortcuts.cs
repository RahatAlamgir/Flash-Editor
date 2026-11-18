using System.Windows;
using System.Windows.Input;
using FlashEditor.App.ViewModels;

namespace FlashEditor.App.Services;

/// <summary>One thing that has a keyboard shortcut: a command (Ctrl+S) or a tool (V).</summary>
/// <param name="Default">The shortcut the program starts with (a gesture such as "Ctrl+Shift+S", or the key of a tool such as "V").</param>
public sealed record ShortcutEntry(string Id, string Section, string Title, string Default, bool IsTool, Func<MainViewModel, ICommand>? Command = null);

/// <summary>
/// All keyboard shortcuts, with the changes the user made (Settings > Keyboard Shortcuts). Commands need Ctrl or Alt (or an F key);
/// tools use a single key. The changes are kept in the settings file.
/// </summary>
public static class Shortcuts
{
    private static MainViewModel? _main;
    private static List<ShortcutEntry> _entries = new();

    /// <summary>Raised after a shortcut was changed.</summary>
    public static event EventHandler? Changed;

    public static IReadOnlyList<ShortcutEntry> Entries => _entries;

    /// <summary>Keys that always keep their job (the tools and the canvas use them), so they cannot be given to a tool.</summary>
    private static readonly HashSet<Key> ReservedToolKeys = new()
    {
        Key.Space, Key.Escape, Key.Delete, Key.Back, Key.Enter, Key.Tab, Key.X, Key.D,
        Key.Left, Key.Right, Key.Up, Key.Down, Key.OemOpenBrackets, Key.OemCloseBrackets,
    };

    /// <summary>Makes the list for this program window (the commands and the tools) and applies the saved changes to the tools.</summary>
    public static void Register(MainViewModel main)
    {
        _main = main;
        var list = new List<ShortcutEntry>();
        void C(string section, string title, string gesture, Func<MainViewModel, ICommand> command)
            => list.Add(new ShortcutEntry("cmd:" + title, section, title, gesture, false, command));

        C("File", "New", "Ctrl+N", m => m.File.NewCommand);
        C("File", "Open", "Ctrl+O", m => m.File.OpenCommand);
        C("File", "Save project", "Ctrl+S", m => m.File.SaveCommand);
        C("File", "Save project as", "Ctrl+Shift+S", m => m.File.SaveAsCommand);
        C("File", "Export as picture", "Ctrl+Alt+S", m => m.File.ExportCommand);
        C("File", "Add page", "Ctrl+Alt+N", m => m.File.AddPageCommand);
        C("File", "Close project", "Ctrl+W", m => m.CloseProjectCommand);
        C("File", "Next picture", "Ctrl+Tab", m => m.NextDocumentCommand);
        C("File", "Previous picture", "Ctrl+Shift+Tab", m => m.PreviousDocumentCommand);

        C("Edit", "Undo", "Ctrl+Z", m => m.Edit.UndoCommand);
        C("Edit", "Redo", "Ctrl+Y", m => m.Edit.RedoCommand);
        C("Edit", "Copy", "Ctrl+C", m => m.Edit.CopyCommand);
        C("Edit", "Cut", "Ctrl+X", m => m.Edit.CutCommand);
        C("Edit", "Paste", "Ctrl+V", m => m.Edit.PasteCommand);
        C("Edit", "Select all", "Ctrl+A", m => m.Edit.SelectAllCommand);
        C("Edit", "Deselect", "Ctrl+Shift+D", m => m.Edit.DeselectCommand);
        C("Edit", "Invert selection", "Ctrl+Shift+I", m => m.Edit.InvertSelectionCommand);

        C("Image", "Image size", "Ctrl+Alt+I", m => m.Image.ImageSizeCommand);
        C("Image", "Canvas size", "Ctrl+Alt+C", m => m.Image.CanvasSizeCommand);
        C("Image", "Remove background (AI)", "Ctrl+Alt+B", m => m.Effects.RemoveBackgroundCommand);
        C("Image", "Remove / fill selection (AI)", "Shift+F5", m => m.Inpaint.FillSelectionCommand);
        C("Image", "Recolor", "Ctrl+Shift+R", m => m.Effects.RecolorCommand);

        C("Layer", "New layer", "Ctrl+Shift+N", m => m.Layers.NewLayerCommand);
        C("Layer", "Duplicate", "Ctrl+D", m => m.Layers.DuplicateCommand);
        C("Layer", "Merge down", "Ctrl+E", m => m.Layers.MergeDownCommand);
        C("Layer", "Lock / unlock", "Ctrl+L", m => m.Layers.ToggleLockCommand);
        C("Layer", "Group", "Ctrl+G", m => m.Layers.GroupCommand);
        C("Layer", "Ungroup", "Ctrl+Shift+G", m => m.Layers.UngroupCommand);
        C("Layer", "Bring forward", "Ctrl+OemCloseBrackets", m => m.Layers.MoveUpCommand);
        C("Layer", "Send backward", "Ctrl+OemOpenBrackets", m => m.Layers.MoveDownCommand);
        C("Layer", "Bring to front", "Ctrl+Shift+OemCloseBrackets", m => m.Layers.BringToFrontCommand);
        C("Layer", "Send to back", "Ctrl+Shift+OemOpenBrackets", m => m.Layers.SendToBackCommand);

        C("View", "Zoom in", "Ctrl+OemPlus", m => m.View.ZoomInCommand);
        C("View", "Zoom out", "Ctrl+OemMinus", m => m.View.ZoomOutCommand);
        C("View", "Fit on screen", "Ctrl+D0", m => m.View.FitCommand);
        C("View", "Actual size (100%)", "Ctrl+D1", m => m.View.ActualSizeCommand);
        C("View", "Rulers", "Ctrl+R", m => m.View.ToggleRulersCommand);
        C("View", "Grid", "Ctrl+OemQuotes", m => m.View.ToggleGridCommand);
        C("View", "Guides", "Ctrl+OemSemicolon", m => m.View.ToggleGuidesCommand);
        C("View", "Snapping", "Ctrl+Shift+OemSemicolon", m => m.View.ToggleSnapCommand);
        C("View", "Keyboard shortcuts", "F1", m => m.View.ShortcutsCommand);

        foreach (var tool in main.Tools.Tools)
            list.Add(new ShortcutEntry("tool:" + tool.Name, "Tools", tool.Name, tool.Shortcut.ToString(), true));
        _entries = list;
        ApplyToTools();
    }

    // ------------------------------------------------------------------ reading

    /// <summary>The shortcut now in use, as text the program can read back ("" = none).</summary>
    public static string Current(ShortcutEntry entry)
        => AppSettings.Current.Shortcuts.TryGetValue(entry.Id, out var text) ? text : entry.Default;

    public static bool IsChanged(ShortcutEntry entry) => AppSettings.Current.Shortcuts.ContainsKey(entry.Id);

    /// <summary>The shortcut as people write it: "Ctrl+Shift+S", "V", "Ctrl++". Empty text for no shortcut.</summary>
    public static string Pretty(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return string.Join("+", text.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(PrettyKey));
    }

    private static string PrettyKey(string name) => name switch
    {
        "OemPlus" => "+",
        "OemMinus" => "-",
        "OemQuotes" => "'",
        "OemSemicolon" => ";",
        "OemOpenBrackets" => "[",
        "OemCloseBrackets" => "]",
        "OemComma" => ",",
        "OemPeriod" => ".",
        "OemQuestion" => "/",
        "OemTilde" => "`",
        "OemBackslash" or "OemPipe" => "\\",
        _ when name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1]) => name[1].ToString(),
        _ => name,
    };

    /// <summary>Turns the keys that were pressed into the text kept in the settings, or null with a reason when they cannot be used.</summary>
    public static string? FromKeys(ShortcutEntry entry, Key key, ModifierKeys modifiers, out string? problem)
    {
        problem = null;
        if (entry.IsTool)
        {
            if (modifiers != ModifierKeys.None) { problem = "A tool shortcut is a single key, without Ctrl, Alt or Shift."; return null; }
            if (ReservedToolKeys.Contains(key)) { problem = $"{PrettyKey(key.ToString())} is used by the program itself. Choose another key."; return null; }
            return key.ToString();
        }
        bool isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (!isFunctionKey && !modifiers.HasFlag(ModifierKeys.Control) && !modifiers.HasFlag(ModifierKeys.Alt))
        {
            problem = "Use Ctrl or Alt together with a key (or one of the F keys).";
            return null;
        }
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    // ------------------------------------------------------------------ changing

    /// <summary>Gives <paramref name="entry"/> a new shortcut (text from <see cref="FromKeys"/>, or "" for none). Another command that had it loses it.</summary>
    /// <returns>The title of the command that lost its shortcut, or null.</returns>
    public static string? Set(ShortcutEntry entry, string text)
    {
        string? taken = null;
        if (text.Length > 0 && !entry.IsTool)
        {
            var other = _entries.FirstOrDefault(e => !e.IsTool && e.Id != entry.Id && string.Equals(Current(e), text, StringComparison.OrdinalIgnoreCase));
            if (other != null) { AppSettings.Current.SetShortcut(other.Id, other.Default == "" ? null : ""); taken = other.Title; }
        }
        AppSettings.Current.SetShortcut(entry.Id, text == entry.Default ? null : text);
        Notify();
        return taken;
    }

    public static void Reset(ShortcutEntry entry)
    {
        AppSettings.Current.SetShortcut(entry.Id, null);
        Notify();
    }

    public static void ResetAll()
    {
        AppSettings.Current.ClearShortcuts();
        Notify();
    }

    private static void Notify()
    {
        ApplyToTools();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ using

    private static void ApplyToTools()
    {
        if (_main == null) return;
        foreach (var entry in _entries.Where(e => e.IsTool))
        {
            var tool = _main.Tools.Tools.FirstOrDefault(t => "tool:" + t.Name == entry.Id);
            if (tool == null) continue;
            string text = Current(entry);
            tool.CustomShortcut = text.Length == 0 ? Key.None : Enum.TryParse<Key>(text, out var key) ? key : tool.Shortcut;
        }
    }

    /// <summary>Puts the shortcuts of all commands on the window (call again after a change).</summary>
    public static void Apply(Window window)
    {
        if (_main == null) return;
        window.InputBindings.Clear();
        var converter = new KeyGestureConverter();
        foreach (var entry in _entries.Where(e => !e.IsTool && e.Command != null))
        {
            string text = Current(entry);
            if (text.Length == 0) continue;
            try
            {
                if (converter.ConvertFromInvariantString(text) is KeyGesture gesture)
                    window.InputBindings.Add(new KeyBinding(entry.Command!(_main), gesture));
            }
            catch (Exception) { /* a damaged entry in the settings file: that shortcut is simply not used */ }
        }
        // The usual extra ways to do the same
        void Alias(string text, ICommand command) => window.InputBindings.Add(new KeyBinding(command, (KeyGesture)converter.ConvertFromInvariantString(text)!));
        Alias("Ctrl+F4", _main.CloseProjectCommand);
        Alias("Ctrl+Shift+Z", _main.Edit.RedoCommand);
        Alias("Ctrl+Add", _main.View.ZoomInCommand);
        Alias("Ctrl+Subtract", _main.View.ZoomOutCommand);
    }
}
