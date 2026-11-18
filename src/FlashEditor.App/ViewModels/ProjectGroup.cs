using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using FlashEditor.Core.Common;
using FlashEditor.App.Services;
using FlashEditor.Core.History;
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

    private HistoryItemViewModel? _selectedHistoryItem;
    private bool _syncingHistory;

    public ProjectGroup()
    {
        Pages.CollectionChanged += OnPagesChanged;
        History.MaxSteps = AppSettings.Current.UndoSteps;
        History.Changed += (_, _) =>
        {
            RebuildHistoryItems();
            Infrastructure.RelayCommand.Refresh();
        };
        StructureHistory.JoinProject(History);
        RebuildHistoryItems();
    }

    /// <summary>
    /// The one undo history of the whole project: the steps of all its pages, in the order they were made. It keeps 30 steps by default (Settings
    /// can change that, up to 60).
    /// </summary>
    public ProjectHistory History { get; } = new();

    /// <summary>Steps about the project itself (deleting a page). They are part of <see cref="History"/> too.</summary>
    public HistoryManager StructureHistory { get; } = new();

    /// <summary>The name of the first row of the History panel (how the project started, e.g. "New Image" or "Open").</summary>
    public string InitialStateName { get; set; } = "Original";

    /// <summary>Raised after Undo / Redo / a jump in the History panel changed something in a page: that page should be shown.</summary>
    public event Action<DocumentViewModel>? PageChanged;

    public bool CanUndo => History.CanUndo;
    public bool CanRedo => History.CanRedo;

    /// <summary>Takes back the newest step of the project (in whichever page it was made).</summary>
    public void Undo() => Show(History.Undo());

    /// <summary>Does the step that was taken back again.</summary>
    public void Redo() => Show(History.Redo());

    /// <summary>Goes to a state in the History panel.</summary>
    public void JumpTo(int position) => Show(History.JumpTo(position));

    private void Show(HistoryManager? owner)
    {
        var page = owner == null ? null : Pages.FirstOrDefault(p => ReferenceEquals(p.Model.History, owner));
        if (page != null) PageChanged?.Invoke(page);
    }

    /// <summary>The rows of the History panel: every step of the project (with the page it was made in, when the project has several).</summary>
    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = new();

    /// <summary>Selecting a row in the History panel jumps to that state.</summary>
    public HistoryItemViewModel? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set
        {
            if (_syncingHistory || value == null || value == _selectedHistoryItem) return;
            JumpTo(value.Position);
        }
    }

    private void RebuildHistoryItems()
    {
        _syncingHistory = true;
        try
        {
            HistoryItems.Clear();
            HistoryItems.Add(new HistoryItemViewModel(InitialStateName, 0, false));
            for (int i = 0; i < History.Count; i++)
            {
                var page = Pages.FirstOrDefault(p => ReferenceEquals(p.Model.History, History.OwnerOf(i)));
                string prefix = page != null && Pages.Count > 1 ? page.PageTitle + ": " : "";
                HistoryItems.Add(new HistoryItemViewModel(prefix + History.ActionAt(i).Name, i + 1, i >= History.Position));
            }
            _selectedHistoryItem = HistoryItems[History.Position];
            OnPropertyChanged(nameof(SelectedHistoryItem));
        }
        finally
        {
            _syncingHistory = false;
        }
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
        RebuildHistoryItems();
    }
}
