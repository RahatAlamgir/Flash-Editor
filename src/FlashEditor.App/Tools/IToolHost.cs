using FlashEditor.App.Services;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Snapping;

namespace FlashEditor.App.Tools;

/// <summary>Everything a tool can reach: the document, colors, dialogs, the canvas and the status bar.</summary>
public interface IToolHost
{
    DocumentViewModel Document { get; }
    ColorsViewModel Colors { get; }
    IDialogService Dialogs { get; }
    ICanvasView? Canvas { get; }

    /// <summary>Snapping for a drag that starts now (null when snapping is off); <paramref name="exclude"/> = the layers being dragged.</summary>
    SnapSession? CreateSnapSession(IEnumerable<Layer> exclude);

    void SelectTool<T>() where T : ToolBase;

    /// <summary>Runs the AI Remove / Fill on the current selection (Remove Brush).</summary>
    void RunAiFill();

    /// <summary>Click Select (AI): selects the object under the click or box (downloads the model first when needed).</summary>
    void RunClickSelect(FlashEditor.AI.Segmentation.ClickPrompt prompt, System.Windows.Input.ModifierKeys modifiers);
    void SetStatus(string message);

    /// <summary>Turns a layer of plain pixels into a picture object (resizable, rotatable, can have a shadow). Undoable.</summary>
    bool MakePictureObject(Layer layer, string actionName = "Make Editable");

    /// <summary>Turns an object layer into pixels (undoable). Does nothing for pixel layers.</summary>
    void ConvertToPixels(Layer layer, bool announce = true);

    /// <summary>Adds a new layer holding the object at the very top (in front of everything) and selects it.</summary>
    Layer AddObjectLayer(LayerObject obj, string actionName);

    /// <summary>Top-most visible, unlocked layer with content at the point (or null). Locked layers are ignored by clicks on the canvas; they can only be selected in the Layers panel.</summary>
    Layer? FindLayerAt(PointD point, double margin = 0, bool includeLocked = false);

    /// <summary>All selected layers (bottom to top); several when Shift+clicking.</summary>
    IReadOnlyList<Layer> SelectedLayers { get; }

    void SelectOnly(Layer? layer);

    /// <summary>Nothing is shown as selected any more (a click outside the canvas).</summary>
    void ClearSelection();

    /// <summary>Selects several layers at once (the last one becomes the active layer).</summary>
    void SetSelection(IEnumerable<Layer> layers, Layer? active);
    void ToggleSelected(Layer layer);

    /// <summary>True while the corner points of the active shape are shown for dragging (reshape).</summary>
    bool IsReshaping { get; }

    /// <summary>Stops showing the corner points of the shape.</summary>
    void StopReshaping();

    /// <summary>The selected part of the active group layer (index in the group's parts), or null.</summary>
    int? ActivePartIndex { get; }

    /// <summary>Selects one part of a group (null = the whole group).</summary>
    void SelectPart(Layer group, int? index);

    /// <summary>Removes the selected part from its group. False when no part is selected.</summary>
    bool DeleteActivePart();

    /// <summary>The AI subject finder, when the model is installed (null otherwise).</summary>
    Func<PixelBuffer, CancellationToken, float[]>? SubjectMask { get; }

    /// <summary>The "keep proportions" switch of the Properties panel: resizing with the handles keeps the width / height ratio (Shift does the opposite).</summary>
    bool KeepProportions { get; }
}
