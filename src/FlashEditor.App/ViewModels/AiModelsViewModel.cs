using System.Collections.ObjectModel;
using FlashEditor.AI.Models;
using FlashEditor.Core.Common;

namespace FlashEditor.App.ViewModels;

/// <summary>The AI Models window: every AI model, what it is for, how much space it uses, and download / delete buttons.</summary>
public sealed class AiModelsViewModel : ObservableObject
{
    public AiModelsViewModel(ModelStore store, Action<ModelInfo> release, Func<string, string, bool> confirm)
    {
        Store = store;
        foreach (var model in ModelCatalog.All)
        {
            var item = new AiModelItem(model, store, release, confirm);
            item.Changed += (_, _) => OnPropertyChanged(nameof(TotalText));
            Items.Add(item);
        }
    }

    public ModelStore Store { get; }

    public ObservableCollection<AiModelItem> Items { get; } = new();

    /// <summary>The folder the models are saved in.</summary>
    public string FolderPath => Store.Folder;

    /// <summary>Total space used by all models.</summary>
    public string TotalText
    {
        get
        {
            long bytes = Items.Sum(i => i.BytesOnDisk);
            int count = Items.Count(i => i.IsInstalled);
            return bytes == 0 ? "No AI models on this PC" : $"{count} of {Items.Count} models on this PC  -  {AiModelItem.FormatSize(bytes)} used";
        }
    }

    /// <summary>Stops downloads that are still running (the window is closing).</summary>
    public void CancelAll()
    {
        foreach (var item in Items) item.CancelDownload();
    }
}
