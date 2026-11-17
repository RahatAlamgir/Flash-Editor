using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using FlashEditor.Core.Common;
using FlashEditor.Core.IO.Project;

namespace FlashEditor.App.ViewModels;

/// <summary>Something whose name is typed in place on a tab (a project or a page).</summary>
public interface IRenamable
{
    /// <summary>True while the name box is shown instead of the name.</summary>
    bool IsRenaming { get; set; }
}

/// <summary>
/// A project: one or more pages (pictures) that are saved together in one .flash file. The project tabs along the top of the editor
/// show projects; the strip at the bottom shows the pages of the selected project. A single picture is a project with one page.
/// </summary>
public sealed class ProjectGroup : ObservableObject, IRenamable
{
    private bool _structureDirty;
    private string? _customName;
    private bool _isRenaming;

    public ProjectGroup()
    {
        Pages.CollectionChanged += OnPagesChanged;
    }

    /// <summary>The pages in the order they are saved.</summary>
    public ObservableCollection<DocumentViewModel> Pages { get; } = new();

    /// <summary>The page that was shown last (what a click on the project tab brings back).</summary>
    public DocumentViewModel? ActivePage { get; set; }

    /// <summary>A name the user typed (null = the file name).</summary>
    public string? CustomName
    {
        get => _customName;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (!SetField(ref _customName, value)) return;
            Refresh();
        }
    }

    public string? FilePath => Pages.FirstOrDefault()?.FilePath;

    /// <summary>What the project tab shows: the name typed by the user, else the file name, else "Untitled".</summary>
    public string Name => _customName ?? (FilePath is { } path
        ? (ProjectFile.IsProject(path) ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path))
        : "Untitled");

    public bool IsDirty => _structureDirty || Pages.Any(p => p.IsPageDirty);

    public string Title => Name + (IsDirty ? " *" : "");

    public string ToolTip => (FilePath ?? "Not saved yet") + "\nDouble-click to rename";

    public bool IsRenaming
    {
        get => _isRenaming;
        set => SetField(ref _isRenaming, value);
    }

    /// <summary>The text in the rename box.</summary>
    public string EditableName
    {
        get => Name;
        set { if (value?.Trim() != Name) CustomName = value; }
    }

    /// <summary>True when pages were added, removed or moved since the last save (their own edits are tracked by each page).</summary>
    public bool StructureDirty
    {
        get => _structureDirty;
        set
        {
            if (_structureDirty == value) return;
            _structureDirty = value;
            foreach (var page in Pages) page.RefreshDirty();
            Refresh();
        }
    }

    /// <summary>Tells the tab that the name or the unsaved mark may have changed.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(EditableName));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(ToolTip));
    }

    // Page names that were not typed ("Page 2") depend on the position, so they change when pages come and go.
    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var page in Pages) page.RefreshTitle();
        Refresh();
    }
}
