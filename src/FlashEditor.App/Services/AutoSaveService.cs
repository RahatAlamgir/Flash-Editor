using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.IO.Project;

namespace FlashEditor.App.Services;

/// <summary>
/// Protects against crashes and power cuts: every minute the pictures with unsaved changes are written to a recovery file in the
/// background. A normal exit deletes it; if it is still there at the next start, the program offers to bring the work back.
/// The user's own files are never touched.
/// </summary>
public sealed class AutoSaveService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly MainViewModel _main;
    private readonly DispatcherTimer _timer;
    private readonly string _file;
    private bool _writing;
    private long _lastStamp = -1;

    public AutoSaveService(MainViewModel main, string? folder = null)
    {
        _main = main;
        folder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashEditor", "recovery");
        _file = Path.Combine(folder, "unsaved-work.flash");
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Interval };
        _timer.Tick += (_, _) => SaveNow();
    }

    /// <summary>Where the recovery file is.</summary>
    public string FilePath => _file;

    public bool HasRecovery => File.Exists(_file);

    public void Start() => _timer.Start();

    /// <summary>Normal exit: nothing to recover any more.</summary>
    public void Stop()
    {
        _timer.Stop();
        Delete();
    }

    public void Delete()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Writes the recovery file now (only when something is unsaved; nothing is written when nothing changed since last time).</summary>
    public void SaveNow()
    {
        if (_writing) return;
        if (Mouse.LeftButton == MouseButtonState.Pressed) return;      // do not compete with a drag or a brush stroke

        var dirty = _main.Documents.Where(d => d.IsDirty).ToList();
        if (dirty.Count == 0) { Delete(); _lastStamp = -1; return; }

        long stamp = dirty.Sum(d => (long)d.Model.History.Position * 31 + d.Model.History.Actions.Count) + dirty.Count * 1000003L;
        if (stamp == _lastStamp && File.Exists(_file)) return;

        // The list of layers and objects is read here, on the interface thread; encoding the pictures happens in the background.
        var pages = dirty.Select(d => new ProjectPage(d.Project.Pages.Count > 1 ? d.DisplayName + " - " + d.PageTitle : d.DisplayName, d.Model, d.VerticalGuides.ToList(), d.HorizontalGuides.ToList())).ToList();
        _writing = true;
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                ProjectWriter.Save(pages, 0, _file);
                _lastStamp = stamp;
            }
            catch (Exception) { /* a failed backup must never disturb the user */ }
            finally { _writing = false; }
        });
    }

    /// <summary>
    /// Offers to bring back the work of a session that did not end normally. Returns the number of pictures restored.
    /// </summary>
    public int OfferRestore(IDialogService dialogs, Func<string, bool>? ask = null)
    {
        if (!HasRecovery) return 0;
        ProjectContent content;
        try { content = ProjectReader.OpenProject(_file); }
        catch (Exception) { Delete(); return 0; }

        string question = $"Flash Editor did not close properly last time.\n\nBring back your unsaved work ({content.Pages.Count} picture{(content.Pages.Count == 1 ? "" : "s")})?";
        bool yes = ask != null ? ask(question) : dialogs.Confirm("Flash Editor", question);
        if (!yes) { Delete(); return 0; }

        foreach (var page in content.Pages)
        {
            var vm = _main.LoadDocument(page.Document, null, "Recovered");
            vm.Project.CustomName = page.Name + " (recovered)";
            vm.VerticalGuides.AddRange(page.VerticalGuides);
            vm.HorizontalGuides.AddRange(page.HorizontalGuides);
            vm.IsRecovered = true;              // counts as unsaved until the user saves it
        }
        Delete();                                // it lives in the open tabs now; the next backup will write it again
        _lastStamp = -1;
        _main.SetStatus($"Brought back {content.Pages.Count} unsaved picture{(content.Pages.Count == 1 ? "" : "s")}. Save them to keep them.");
        return content.Pages.Count;
    }
}
