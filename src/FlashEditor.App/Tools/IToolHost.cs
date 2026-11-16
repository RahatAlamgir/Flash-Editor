using FlashEditor.App.Services;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Tools;

/// <summary>Everything a tool can reach: the document, colors, dialogs, the canvas and the status bar.</summary>
public interface IToolHost
{
    DocumentViewModel Document { get; }
    ColorsViewModel Colors { get; }
    IDialogService Dialogs { get; }
    ICanvasView? Canvas { get; }

    void SelectTool<T>() where T : ToolBase;
    void SetStatus(string message);

    /// <summary>Turns an object layer into pixels (undoable). Does nothing for pixel layers.</summary>
    void ConvertToPixels(Layer layer, bool announce = true);

    /// <summary>Adds a new layer holding the object, above the active layer, and selects it.</summary>
    Layer AddObjectLayer(LayerObject obj, string actionName);

    /// <summary>Top-most visible layer with content at the point (or null).</summary>
    Layer? FindLayerAt(PointD point, double margin = 0);

    /// <summary>All selected layers (bottom to top); several when Shift+clicking.</summary>
    IReadOnlyList<Layer> SelectedLayers { get; }

    void SelectOnly(Layer? layer);
    void ToggleSelected(Layer layer);

    /// <summary>The AI subject finder, when the model is installed (null otherwise).</summary>
    Func<PixelBuffer, CancellationToken, float[]>? SubjectMask { get; }
}
