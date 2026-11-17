using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.History;

/// <summary>The history forgets its oldest steps when the pixel copies kept for undo take too much memory.</summary>
public class HistoryMemoryTests
{
    private sealed class FakeStep : IHistoryAction
    {
        public FakeStep(string name, long bytes) { Name = name; SizeInBytes = bytes; }
        public string Name { get; }
        public long SizeInBytes { get; }
        public void Undo() { }
        public void Redo() { }
    }

    [Fact]
    public void Oldest_steps_are_dropped_when_over_the_memory_budget()
    {
        var history = new HistoryManager { MaxBytes = 100 };
        for (int i = 1; i <= 5; i++) history.Push(new FakeStep($"step {i}", 40));

        // 5 x 40 = 200 bytes; the budget allows 2 steps (80 bytes).
        Assert.Equal(new[] { "step 4", "step 5" }, history.Actions.Select(a => a.Name));
        Assert.Equal(2, history.Position);
        Assert.True(history.TotalBytes <= 100);
        Assert.True(history.CanUndo);
    }

    [Fact]
    public void The_newest_step_is_always_kept_even_when_it_alone_is_over_budget()
    {
        var history = new HistoryManager { MaxBytes = 100 };
        history.Push(new FakeStep("small", 10));
        history.Push(new FakeStep("huge", 500));

        Assert.Equal(new[] { "huge" }, history.Actions.Select(a => a.Name));
        Assert.Equal(1, history.Position);
        history.Undo();
        history.Redo();
        Assert.Equal(1, history.Position);
    }

    [Fact]
    public void Steps_without_a_size_never_count_against_the_budget()
    {
        var history = new HistoryManager { MaxBytes = 10 };
        for (int i = 0; i < 30; i++) history.Push(new FakeStep($"s{i}", 0));
        Assert.Equal(30, history.Actions.Count);
    }

    [Fact]
    public void Dropping_old_steps_keeps_the_unsaved_changes_flag_right()
    {
        var history = new HistoryManager { MaxBytes = 100 };
        history.Push(new FakeStep("a", 40));
        history.MarkSaved();
        Assert.False(history.IsDirty);

        history.Push(new FakeStep("b", 40));
        history.Push(new FakeStep("c", 40)); // over budget: step "a" is forgotten
        Assert.True(history.IsDirty);        // the picture was saved after "a", and "b" and "c" came later

        history.Undo();
        Assert.True(history.IsDirty);
        history.Undo();                      // back at the oldest state we still have = the state that was saved
        Assert.False(history.IsDirty);
        Assert.False(history.CanUndo);       // and undo cannot go further back than that
    }

    [Fact]
    public void Forgetting_the_saved_state_makes_the_picture_stay_marked_as_changed()
    {
        var history = new HistoryManager { MaxBytes = 70 };
        history.Push(new FakeStep("a", 40));
        history.Push(new FakeStep("b", 40)); // "a" forgotten; saved state below is the one BEFORE "a"
        history.MarkSaved();
        // Saved at position 1 ("after b"); now two more steps push the saved state out of reach.
        history.Push(new FakeStep("c", 40));
        history.Push(new FakeStep("d", 40));
        history.Undo();
        history.Undo();
        history.Undo();
        Assert.False(history.CanUndo);
        Assert.True(history.IsDirty);        // "after b" is gone, so the oldest state is not the saved one
    }

    [Fact]
    public void Real_pixel_steps_report_the_memory_they_hold()
    {
        var layer = new Layer("L", 100, 80);
        var before = layer.Pixels.Clone();
        layer.Pixels.SetPixel(5, 5, ColorRgba.White);
        var step = PixelChangeAction.Create("edit", layer, before, new PixelRect(0, 0, 100, 80))!;

        Assert.Equal(2L * 100 * 80 * 4, step.SizeInBytes);   // a before and an after copy of the changed area
    }

    [Fact]
    public void A_whole_image_edit_of_a_big_picture_cannot_pile_up_without_limit()
    {
        var doc = Document.FromImage(new PixelBuffer(2000, 1500)); // 12 MB per copy
        var layer = doc.Layers[0];
        doc.History.MaxBytes = 100L * 1024 * 1024;                 // 100 MB budget for this test

        for (int i = 0; i < 20; i++)
        {
            var before = layer.Pixels.Clone();
            layer.Pixels.SetPixel(i, i, ColorRgba.White);
            layer.NotifyPixelsChanged();
            doc.History.Push(PixelChangeAction.Create("full edit", layer, before, layer.Pixels.Bounds)!);
        }

        Assert.True(doc.History.TotalBytes <= doc.History.MaxBytes);
        Assert.InRange(doc.History.Actions.Count, 3, 5);           // 24 MB per step: about 4 fit in 100 MB
    }
}
