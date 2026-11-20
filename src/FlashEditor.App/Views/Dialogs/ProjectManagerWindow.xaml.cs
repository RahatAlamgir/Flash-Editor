using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlashEditor.App.Services;
using FlashEditor.Core.IO;
using FlashEditor.Core.IO.Project;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// The Project Manager: every project as a card with its picture, to open, create, rename or delete. It is the first thing shown when
/// the program starts, and is also reachable from File > Project Manager.
/// </summary>
public partial class ProjectManagerWindow : Window
{
    private readonly ProjectLibrary _library;
    private readonly IDialogService _dialogs;
    private List<ProjectEntry> _all = new();

    public ProjectManagerWindow(ProjectLibrary library, IDialogService dialogs, bool atStartup)
    {
        InitializeComponent();
        _library = library;
        _dialogs = dialogs;
        if (!atStartup) WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowFolder();
        Loaded += async (_, _) => await ReloadAsync();
    }

    /// <summary>What the user chose (set when the window closes with OK).</summary>
    public ProjectManagerResult? Result { get; private set; }

    private ProjectEntry? Selected => ProjectList.SelectedItem as ProjectEntry;

    // ------------------------------------------------------------------ the list

    private async Task ReloadAsync()
    {
        LoadingText.Visibility = Visibility.Visible;
        EmptyPanel.Visibility = Visibility.Collapsed;
        _all = await Task.Run(() => _library.Load());
        LoadingText.Visibility = Visibility.Collapsed;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string text = SearchBox.Text.Trim();
        var shown = _all.Where(e => text.Length == 0 || e.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        ProjectList.ItemsSource = new ObservableCollection<ProjectEntry>(shown);
        if (shown.Count > 0) ProjectList.SelectedIndex = 0;

        bool empty = shown.Count == 0;
        EmptyPanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            bool searching = text.Length > 0 && _all.Count > 0;
            EmptyTitle.Text = searching ? "No project matches" : "No projects yet";
            EmptyText.Text = searching ? "Try another part of the name." : "Click New project to start one. Everything you save appears here.";
        }
        SubtitleText.Text = _all.Count == 0 ? "Everything you made with Flash Editor"
            : $"{_all.Count} project{(_all.Count == 1 ? "" : "s")}";
    }

    private void ShowFolder() => FolderText.Text = "New projects go to: " + _library.ProjectsFolder;

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilter();
    }

    // ------------------------------------------------------------------ choosing

    private void Finish(ProjectManagerResult result)
    {
        Result = result;
        DialogResult = true;
    }

    private void OpenEntry(ProjectEntry? entry)
    {
        if (entry == null) return;
        if (entry.Error != null)
        {
            _dialogs.ShowError($"\"{entry.Name}\" could not be read. The file may be damaged.");
            return;
        }
        Finish(new ProjectManagerResult(ProjectManagerAction.Open, entry.Path));
    }


    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(ProjectList, source) is ListBoxItem item)
            OpenEntry(item.DataContext as ProjectEntry);
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Selected != null) { DeleteEntry(Selected); e.Handled = true; }
        else if (e.Key == Key.Enter && Selected != null) { OpenEntry(Selected); e.Handled = true; }
    }

    /// <summary>Esc closes the manager (at start-up that quits the program).</summary>
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !(e.OriginalSource is TextBox { Text.Length: > 0 })) { DialogResult = false; e.Handled = true; }
    }

    private void OnNewProject(object sender, RoutedEventArgs e)
    {
        var settings = _dialogs.ShowNewProject(FlashEditor.Core.Imaging.ColorRgba.White);
        if (settings != null) Finish(new ProjectManagerResult(ProjectManagerAction.New, New: settings));
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Open", Filter = ImageFileFormats.OpenAnyFilter };
        if (dialog.ShowDialog(this) == true)
            Finish(new ProjectManagerResult(ProjectManagerAction.Open, dialog.FileName));
    }

    // ------------------------------------------------------------------ folder

    private void OnShowFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_library.ProjectsFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_library.ProjectsFolder}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            _dialogs.ShowError("The folder could not be opened.");
        }
    }

    private async void OnChangeFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Folder for new projects", InitialDirectory = _library.ProjectsFolder };
        if (dialog.ShowDialog(this) != true) return;
        _library.ProjectsFolder = dialog.FolderName;
        ShowFolder();
        await ReloadAsync();
    }

    // ------------------------------------------------------------------ right-click menu

    // The right-click menu gets the card's own data, so the entry is the one that was clicked.
    private ProjectEntry? MenuEntry(object sender) => (sender as MenuItem)?.DataContext as ProjectEntry ?? Selected;

    private void OnMenuOpen(object sender, RoutedEventArgs e) => OpenEntry(MenuEntry(sender));

    private void OnMenuShow(object sender, RoutedEventArgs e)
    {
        if (MenuEntry(sender) is { } entry)
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{entry.Path}\"") { UseShellExecute = true });
    }

    private async void OnMenuRename(object sender, RoutedEventArgs e)
    {
        if (MenuEntry(sender) is not { } entry) return;
        var name = _dialogs.ShowTextInput("Rename Project", "New name:", entry.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        string clean = ProjectLibrary.CleanName(name);
        string target = Path.Combine(Path.GetDirectoryName(entry.Path)!, clean + ProjectFile.Extension);
        if (string.Equals(target, entry.Path, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(target)) { _dialogs.ShowError($"There is already a project called \"{clean}\" in that folder."); return; }
        try
        {
            File.Move(entry.Path, target);
            _library.Forget(entry.Path);
            _library.AddRecent(target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("The project could not be renamed. Is it open in another program?");
            return;
        }
        await ReloadAsync();
    }

    private async void OnMenuForget(object sender, RoutedEventArgs e)
    {
        if (MenuEntry(sender) is not { } entry) return;
        _library.Forget(entry.Path);
        // A project that lives in the projects folder is listed because of the folder, so forgetting it only works for others.
        await ReloadAsync();
    }

    private void OnMenuDelete(object sender, RoutedEventArgs e)
    {
        if (MenuEntry(sender) is { } entry) DeleteEntry(entry);
    }

    private async void DeleteEntry(ProjectEntry entry)
    {
        if (!_dialogs.Confirm("Delete project", $"Move \"{entry.Name}\" to the Recycle Bin?\n\n{entry.Path}")) return;
        try
        {
            FileSystem.DeleteFile(entry.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            _library.Forget(entry.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _dialogs.ShowError("The project could not be deleted. Is it open in another program?");
        }
        await ReloadAsync();
    }
}
