using FlashEditor.Core.History;

namespace FlashEditor.Core.Tests.History;

public class ProjectHistoryTests
{
    private sealed class Step : IHistoryAction
    {
        private readonly List<string> _log;
        public Step(string name, List<string> log) { Name = name; _log = log; }
        public string Name { get; }
        public void Undo() => _log.Add("undo " + Name);
        public void Redo() => _log.Add("redo " + Name);
    }

    /// <summary>A step that folds into the one before it when the names match (like many small opacity changes).</summary>
    private sealed class Mergeable : IHistoryAction
    {
        public Mergeable(string name) { Name = name; }
        public string Name { get; }
        public void Undo() { }
        public void Redo() { }
        public bool TryMerge(IHistoryAction next) => next is Mergeable m && m.Name == Name;
    }

    private readonly List<string> _log = new();
    private readonly ProjectHistory _project = new();
    private readonly HistoryManager _a = new(), _b = new();

    public ProjectHistoryTests()
    {
        _a.JoinProject(_project);
        _b.JoinProject(_project);
    }

    private void Push(HistoryManager history, string name) => history.Push(new Step(name, _log));

    [Fact]
    public void By_default_thirty_steps_are_kept_and_the_most_that_can_be_chosen_is_sixty()
    {
        Assert.Equal(30, _project.MaxSteps);
        _project.MaxSteps = 500;
        Assert.Equal(60, _project.MaxSteps);
        _project.MaxSteps = 0;
        Assert.Equal(1, _project.MaxSteps);
        Assert.Equal(60, ProjectHistory.ClampSteps(61));
    }

    [Fact]
    public void Undo_takes_back_the_newest_step_of_the_whole_project_whichever_page_it_was_made_in()
    {
        Push(_a, "a1"); Push(_b, "b1"); Push(_a, "a2");
        Assert.Same(_a, _project.Undo());
        Assert.Same(_b, _project.Undo());
        Assert.Same(_a, _project.Undo());
        Assert.Equal(new[] { "undo a2", "undo b1", "undo a1" }, _log);
        Assert.False(_project.CanUndo);
        Assert.Same(_a, _project.Redo());
        Assert.Same(_b, _project.Redo());
        Assert.Equal(new[] { "undo a2", "undo b1", "undo a1", "redo a1", "redo b1" }, _log);
    }

    [Fact]
    public void A_page_that_was_not_touched_keeps_its_own_state_when_another_page_is_undone()
    {
        Push(_a, "a1"); Push(_b, "b1");
        _project.Undo();                                    // takes back b1 only
        Assert.True(_a.IsDirty);
        Assert.False(_b.IsDirty);
        Assert.Equal(1, _a.Position);
        Assert.Equal(0, _b.Position);
    }

    [Fact]
    public void The_oldest_steps_are_forgotten_when_there_are_more_than_the_limit()
    {
        _project.MaxSteps = 3;
        Push(_a, "a1"); Push(_b, "b1"); Push(_a, "a2"); Push(_b, "b2"); Push(_a, "a3");
        Assert.Equal(3, _project.Count);
        Assert.Equal(3, _project.Position);
        // a1 and b1 are gone from their pages too
        Assert.Equal(new[] { "a2", "a3" }, _a.Actions.Select(x => x.Name));
        Assert.Equal(new[] { "b2" }, _b.Actions.Select(x => x.Name));
        Assert.Equal("a2", _project.ActionAt(0).Name);
        Assert.Equal("b2", _project.ActionAt(1).Name);
        Assert.Equal("a3", _project.ActionAt(2).Name);
    }

    [Fact]
    public void Lowering_the_limit_forgets_the_oldest_steps_at_once()
    {
        for (int i = 0; i < 10; i++) Push(i % 2 == 0 ? _a : _b, "s" + i);
        _project.MaxSteps = 4;
        Assert.Equal(4, _project.Count);
        Assert.Equal("s6", _project.ActionAt(0).Name);
        Assert.Equal("s9", _project.ActionAt(3).Name);
    }

    [Fact]
    public void New_work_removes_what_could_have_been_redone_in_every_page()
    {
        Push(_a, "a1"); Push(_b, "b1");
        _project.Undo();                                    // b1 could be redone
        Assert.True(_b.CanRedo);
        Push(_a, "a2");                                     // new work in the other page
        Assert.False(_b.CanRedo);
        Assert.False(_project.CanRedo);
        Assert.Equal(2, _project.Count);
        Assert.Equal(new[] { "a1", "a2" }, _a.Actions.Select(x => x.Name));
        Assert.Empty(_b.Actions);
    }

    [Fact]
    public void Steps_do_not_fold_together_when_a_step_of_another_page_came_in_between()
    {
        _a.Push(new Mergeable("opacity"));
        _a.Push(new Mergeable("opacity"));                  // right after: folds into one step
        Assert.Equal(1, _project.Count);
        Push(_b, "b1");
        _a.Push(new Mergeable("opacity"));                  // b1 is newer now: must stay a step of its own
        Assert.Equal(3, _project.Count);
        Assert.Equal(2, _a.Actions.Count);
    }

    [Fact]
    public void Jumping_goes_back_and_forth_through_the_steps_of_all_pages()
    {
        Push(_a, "a1"); Push(_b, "b1"); Push(_a, "a2");
        _project.JumpTo(0);
        Assert.Equal(0, _a.Position + _b.Position);
        _project.JumpTo(2);
        Assert.Equal(1, _a.Position);
        Assert.Equal(1, _b.Position);
        Assert.Equal(new[] { "undo a2", "undo b1", "undo a1", "redo a1", "redo b1" }, _log);
    }

    [Fact]
    public void A_page_that_leaves_the_project_takes_its_steps_with_it()
    {
        Push(_a, "a1"); Push(_b, "b1"); Push(_a, "a2");
        _b.JoinProject(null);
        Assert.Equal(2, _project.Count);
        Assert.Same(_a, _project.Undo());
        Assert.Same(_a, _project.Undo());
    }

    [Fact]
    public void A_history_without_a_project_works_as_before()
    {
        var alone = new HistoryManager();
        Push(alone, "x1"); Push(alone, "x2");
        alone.Undo();
        Assert.Equal(1, alone.Position);
        Assert.True(alone.CanRedo);
    }
}
