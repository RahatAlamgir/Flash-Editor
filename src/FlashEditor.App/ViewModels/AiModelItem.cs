using System.IO;
using System.Net.Http;
using FlashEditor.AI.Models;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;

namespace FlashEditor.App.ViewModels;

/// <summary>One row of the AI Models window: a model, whether it is on this PC, and the buttons to download or delete it.</summary>
public sealed class AiModelItem : ObservableObject
{
    private readonly ModelStore _store;
    private readonly Action<ModelInfo> _release;
    private readonly Func<string, string, bool> _confirm;
    private CancellationTokenSource? _download;
    private bool _isInstalled;
    private bool _isBusy;
    private double _progress;
    private string _message = "";
    private long _bytesOnDisk;

    /// <param name="release">Lets go of the model in memory (so its file can be deleted).</param>
    /// <param name="confirm">Asks a yes / no question (title, text).</param>
    public AiModelItem(ModelInfo model, ModelStore store, Action<ModelInfo> release, Func<string, string, bool> confirm)
    {
        Model = model;
        _store = store;
        _release = release;
        _confirm = confirm;
        var description = ModelCatalog.Describe(model);
        Title = description.Title;
        UsedFor = description.UsedFor;
        DownloadCommand = new RelayCommand(() => _ = DownloadAsync(), () => !IsInstalled && !IsBusy);
        DeleteCommand = new RelayCommand(Delete, () => (IsInstalled || BytesOnDisk > 0) && !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        Refresh();
    }

    public ModelInfo Model { get; }
    public string Title { get; }
    public string UsedFor { get; }

    public RelayCommand DownloadCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>Raised after the model was downloaded or deleted (the window updates its totals).</summary>
    public event EventHandler? Changed;

    public bool IsInstalled
    {
        get => _isInstalled;
        private set
        {
            if (SetField(ref _isInstalled, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    /// <summary>Space the model's files use on this PC.</summary>
    public long BytesOnDisk
    {
        get => _bytesOnDisk;
        private set => SetField(ref _bytesOnDisk, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanDownload));
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>0..1 while a download runs.</summary>
    public double Progress
    {
        get => _progress;
        private set
        {
            if (SetField(ref _progress, value)) OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>A problem to show (the download failed...), or empty.</summary>
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    public bool CanDownload => !IsInstalled && !IsBusy;
    public bool CanDelete => (IsInstalled || BytesOnDisk > 0) && !IsBusy;

    public string StatusText
    {
        get
        {
            if (IsBusy) return $"Downloading... {Progress:P0}";
            if (IsInstalled) return $"On this PC  -  {FormatSize(BytesOnDisk)}";
            return BytesOnDisk > 0 ? $"Not complete ({FormatSize(BytesOnDisk)} left over)  -  download is {Model.SizeText}" : $"Not downloaded  -  download is {Model.SizeText}";
        }
    }

    /// <summary>Looks at the disk again.</summary>
    public void Refresh()
    {
        IsInstalled = _store.IsInstalled(Model);
        BytesOnDisk = _store.BytesOnDisk(Model);
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanDelete));
        RelayCommand.Refresh();
    }

    private async Task DownloadAsync()
    {
        Message = "";
        using var cancel = new CancellationTokenSource();
        _download = cancel;
        Progress = 0;
        IsBusy = true;
        try
        {
            var progress = new Progress<double>(p => Progress = p);
            await _store.DownloadAsync(Model, progress, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            _store.DeletePartialDownload(Model);
            Message = "Download cancelled.";
        }
        catch (Exception e)
        {
            _store.DeletePartialDownload(Model);
            Message = e is HttpRequestException ? "The download failed. Check your internet connection and try again." : e.Message;
        }
        finally
        {
            _download = null;
            IsBusy = false;
            Refresh();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Cancel() => _download?.Cancel();

    private void Delete()
    {
        string text = $"Delete the {Title.ToLowerInvariant()} model from this PC?\n\n" +
                      $"It frees {FormatSize(BytesOnDisk)}. The features that use it ({UsedFor}) will ask to download it again the next time you use them.";
        if (!_confirm("Delete AI model", text)) return;

        Message = "";
        try
        {
            _release(Model);                 // a model that is loaded in memory keeps its file open
            _store.Delete(Model);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Message = "The model could not be deleted because it is in use. Close any running AI operation and try again.";
        }
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops a running download (when the window is closed).</summary>
    public void CancelDownload() => _download?.Cancel();

    public static string FormatSize(long bytes)
        => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024.0 * 1024 * 1024):0.0} GB" : $"{bytes / (1024.0 * 1024):0} MB";
}
