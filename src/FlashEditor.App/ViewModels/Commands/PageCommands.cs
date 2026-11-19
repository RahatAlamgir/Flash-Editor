using System.Windows.Input;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>
/// Working with the pages of a project like in Canva: pick several pages (Ctrl+click, Shift+click), copy, paste (also into another project),
/// duplicate and delete them all at once, and add or paste pages between other pages.
/// </summary>
public sealed class PageCommands : ObservableObject
{
    /// <summary>A copied page: its own copy of everything on it, so later changes to the original do not change the clipboard.</summary>
    private sealed record PageClip(Document Model, string Name, List<double> VerticalGuides, List<double> HorizontalGuides);

    private readonly MainViewModel _main;
    private List<PageClip> _clipboard = new();
    private DocumentViewModel? _anchor;

    public PageCommands(MainViewModel main)
    {
        _main = main;
        // The page that is shown is always part of the selection (unless several were picked on purpose and it is one of them).
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Document)) EnsureCurrentSelected();
        };
        EnsureCurrentSelected();
    }

    private DocumentViewModel Current => _main.Document;
    private ProjectGroup Group => _main.Document.Project;

    /// <summary>True when copied pages are waiting to be pasted.</summary>
    public bool HasClipboard => _clipboard.Count > 0;

    // ------------------------------------------------------------------ selection

    private void EnsureCurrentSelected()
    {
        var current = _main.Document;
        if (current.IsPageSelected) return;
        foreach (var other in current.Project.Pages) other.IsPageSelected = false;
        current.IsPageSelected = true;
        _anchor = current;
    }

    /// <summary>The selected pages in project order (the page that is shown when none is selected).</summary>
    public IReadOnlyList<DocumentViewModel> Selected()
    {
        var picked = Group.Pages.Where(p => p.IsPageSelected).ToList();
        return picked.Count > 0 ? picked : new List<DocumentViewModel> { Current };
    }

    /// <summary>A click on a page: plain = open it alone, Ctrl = add / remove it, Shift = everything from the last clicked page to this one.</summary>
    public void Click(DocumentViewModel page, ModifierKeys modifiers)
    {
        var pages = page.Project.Pages;
        if (modifiers.HasFlag(ModifierKeys.Shift) && _anchor != null && pages.Contains(_anchor))
        {
            int a = pages.IndexOf(_anchor), b = pages.IndexOf(page);
            for (int i = 0; i < pages.Count; i++) pages[i].IsPageSelected = i >= Math.Min(a, b) && i <= Math.Max(a, b);
            return;
        }
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            page.IsPageSelected = !page.IsPageSelected;
            if (!pages.Any(p => p.IsPageSelected)) page.IsPageSelected = true;
            _anchor = page;
            return;
        }
        foreach (var other in pages) other.IsPageSelected = ReferenceEquals(other, page);
        _anchor = page;
        _main.ShowDocument(page);
    }

    public void SelectAll()
    {
        foreach (var page in Group.Pages) page.IsPageSelected = true;
        _main.SetStatus($"Selected all {Group.Pages.Count} pages. Ctrl+C copies them, Ctrl+D duplicates them, Delete removes them.");
    }

    // ------------------------------------------------------------------ copy and paste (also between projects)

    public void Copy()
    {
        var pages = Selected();
        _clipboard = pages.Select(p => new PageClip(DocumentCopy.Copy(p.Model), p.PageTitle, p.VerticalGuides.ToList(), p.HorizontalGuides.ToList())).ToList();
        OnPropertyChanged(nameof(HasClipboard));
        _main.SetStatus(pages.Count == 1
            ? $"Copied \"{pages[0].PageTitle}\". Click Paste, press Ctrl+V, or right-click between pages; you can switch to another project first."
            : $"Copied {pages.Count} pages. Click Paste, press Ctrl+V, or right-click between pages; you can switch to another project first.");
    }

    /// <summary>Puts the copied pages into the current project at <paramref name="index"/> (0 = first). Returns false when nothing was copied.</summary>
    public bool Paste(int? index = null)
    {
        if (_clipboard.Count == 0)
        {
            _main.SetStatus("No pages copied yet. Select pages, then Copy.");
            return false;
        }
        var group = Group;
        int at = Math.Clamp(index ?? group.Pages.IndexOf(Current) + 1, 0, group.Pages.Count);
        var added = new List<DocumentViewModel>();
        var steps = new List<IHistoryAction?>();
        foreach (var clip in _clipboard)
        {
            var page = _main.InsertPage(group, at + added.Count, DocumentCopy.Copy(clip.Model), UniqueName(group, clip.Name), show: false);
            page.VerticalGuides.AddRange(clip.VerticalGuides);
            page.HorizontalGuides.AddRange(clip.HorizontalGuides);
            steps.Add(new InsertPageAction(_main, group, page, at + added.Count));
            added.Add(page);
        }
        group.StructureHistory.Push(new CompositeAction(added.Count == 1 ? "Paste page" : $"Paste {added.Count} pages", steps));
        SelectOnly(added);
        _main.ShowDocument(added[0]);
        _main.SetStatus(added.Count == 1 ? $"Pasted \"{added[0].PageTitle}\"." : $"Pasted {added.Count} pages.");
        return true;
    }

    private static string UniqueName(ProjectGroup group, string name)
    {
        var used = group.Pages.Select(p => p.PageTitle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name)) return name;
        string candidate = name + " copy";
        for (int i = 2; used.Contains(candidate); i++) candidate = $"{name} copy {i}";
        return candidate;
    }

    private void SelectOnly(IEnumerable<DocumentViewModel> pages)
    {
        var chosen = pages.ToHashSet();
        foreach (var page in Group.Pages) page.IsPageSelected = chosen.Contains(page);
        _anchor = chosen.FirstOrDefault();
    }

    // ------------------------------------------------------------------ duplicate, add, delete

    /// <summary>Copies of all selected pages, put together right after the last selected page.</summary>
    public void Duplicate()
    {
        var group = Group;
        var pages = Selected();
        int at = group.Pages.IndexOf(pages[^1]) + 1;
        var added = new List<DocumentViewModel>();
        var steps = new List<IHistoryAction?>();
        foreach (var source in pages)
        {
            var page = _main.InsertPage(group, at + added.Count, DocumentCopy.Copy(source.Model), UniqueName(group, source.PageTitle), show: false);
            page.VerticalGuides.AddRange(source.VerticalGuides);
            page.HorizontalGuides.AddRange(source.HorizontalGuides);
            steps.Add(new InsertPageAction(_main, group, page, at + added.Count));
            added.Add(page);
        }
        group.StructureHistory.Push(new CompositeAction(added.Count == 1 ? "Duplicate page" : $"Duplicate {added.Count} pages", steps));
        SelectOnly(added);
        _main.ShowDocument(added[0]);
        _main.SetStatus(added.Count == 1 ? $"Duplicated the page as \"{added[0].PageTitle}\"." : $"Duplicated {added.Count} pages.");
    }

    /// <summary>A new empty page (the size of the page next to it) at <paramref name="index"/>.</summary>
    public void AddBlank(int index)
    {
        var group = Group;
        index = Math.Clamp(index, 0, group.Pages.Count);
        var neighbor = group.Pages[Math.Clamp(index > 0 ? index - 1 : 0, 0, group.Pages.Count - 1)];
        var used = group.Pages.Select(p => p.PageTitle).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int number = group.Pages.Count + 1;
        while (used.Contains($"Page {number}")) number++;
        var page = _main.InsertPage(group, index, Document.CreateBlank(neighbor.Model.Width, neighbor.Model.Height, ColorRgba.White), $"Page {number}", show: false);
        group.StructureHistory.Push(new InsertPageAction(_main, group, page, index));
        SelectOnly(new[] { page });
        _main.ShowDocument(page);
        _main.SetStatus($"Added {page.PageTitle}.");
    }

    /// <summary>Deletes all selected pages after one question. A project keeps at least one page.</summary>
    public void Delete()
    {
        var group = Group;
        var pages = Selected();
        if (pages.Count == 1)
        {
            _main.CloseDocument(pages[0]);
            return;
        }
        if (pages.Count >= group.Pages.Count)
        {
            _main.Dialogs.ShowInfo("Delete pages", "A project needs at least one page. Leave one page unselected, or close the project instead.");
            return;
        }
        if (!_main.Dialogs.Confirm("Delete pages", $"Delete these {pages.Count} pages from this project?")) return;

        var steps = new List<IHistoryAction?>();
        foreach (var page in pages)
        {
            int index = group.Pages.IndexOf(page);
            _main.DeletePageWithoutAsking(group, page);
            steps.Add(new DeletePageAction(_main, group, page, index));
        }
        group.StructureHistory.Push(new CompositeAction($"Delete {pages.Count} pages", steps));
        EnsureCurrentSelected();
        _main.SetStatus($"Deleted {pages.Count} pages. Ctrl+Z brings them back.");
    }

    // ------------------------------------------------------------------ menu of a page (strip and grid view)

    /// <summary>What a choice in a page menu does. Copy, duplicate and delete work on all selected pages (a page that is not selected is selected first).</summary>
    public void Run(DocumentViewModel page, string action, bool askNameInDialog = false)
    {
        var pages = page.Project.Pages;
        int index = pages.IndexOf(page);
        if (action is "copy" or "duplicate" or "delete" && !page.IsPageSelected) Click(page, ModifierKeys.None);
        switch (action)
        {
            case "open":
                Click(page, ModifierKeys.None);
                _main.ShowPageGrid = false;
                break;
            case "rename":
                Click(page, ModifierKeys.None);
                if (askNameInDialog)
                {
                    var name = _main.Dialogs.ShowTextInput("Rename Page", "Name of the page:", page.PageTitle);
                    if (!string.IsNullOrWhiteSpace(name)) page.CustomName = name;
                }
                else _main.RenameDocumentCommand.Execute(page);
                break;
            case "copy": Copy(); break;
            case "paste-before": Click(page, ModifierKeys.None); Paste(index); break;
            case "paste-after": Click(page, ModifierKeys.None); Paste(index + 1); break;
            case "add-before": Click(page, ModifierKeys.None); AddBlank(index); break;
            case "add-after": Click(page, ModifierKeys.None); AddBlank(index + 1); break;
            case "duplicate": Duplicate(); break;
            case "left": if (index > 0) _main.MovePage(page, index - 1); break;
            case "right": if (index >= 0 && index < pages.Count - 1) _main.MovePage(page, index + 1); break;
            case "delete": Delete(); break;
        }
    }
}
