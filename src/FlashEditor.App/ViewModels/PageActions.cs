using FlashEditor.Core.History;

namespace FlashEditor.App.ViewModels;

/// <summary>Deleting a page of a project, as an undo step: Undo puts the page back where it was (with all its layers and its own history).</summary>
public sealed class DeletePageAction : IHistoryAction
{
    private readonly MainViewModel _main;
    private readonly ProjectGroup _group;
    private readonly DocumentViewModel _page;
    private readonly int _index;

    public DeletePageAction(MainViewModel main, ProjectGroup group, DocumentViewModel page, int index)
    {
        _main = main;
        _group = group;
        _page = page;
        _index = index;
    }

    public string Name => $"Delete page \"{_page.PageTitle}\"";

    public void Undo() => _main.RestorePage(_group, _page, _index);

    public void Redo() => _main.DeletePageWithoutAsking(_group, _page);

    /// <summary>The deleted page stays in memory while this step can still be undone.</summary>
    public long SizeInBytes => _page.Model.Layers.Sum(layer => (long)layer.Pixels.Data.Length);
}

/// <summary>Adding (or pasting, or duplicating) a page, as an undo step: Undo takes it out again, Redo puts it back at the same place.</summary>
public sealed class InsertPageAction : IHistoryAction
{
    private readonly MainViewModel _main;
    private readonly ProjectGroup _group;
    private readonly DocumentViewModel _page;
    private readonly int _index;

    public InsertPageAction(MainViewModel main, ProjectGroup group, DocumentViewModel page, int index)
    {
        _main = main;
        _group = group;
        _page = page;
        _index = index;
    }

    public string Name => $"Add page \"{_page.PageTitle}\"";

    public void Undo() => _main.DeletePageWithoutAsking(_group, _page);

    public void Redo() => _main.RestorePage(_group, _page, _index);

    public long SizeInBytes => _page.Model.Layers.Sum(layer => (long)layer.Pixels.Data.Length);
}
