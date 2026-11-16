using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.App.Tools.Selecting;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Tools.Text;

/// <summary>
/// Click on the canvas and type: the text becomes an editable text object on its own layer.
/// Change it later in the Properties panel, or double-click it with the Move tool.
/// </summary>
public sealed class TextTool : ToolBase
{
    private readonly TextToolOptions _options = new();
    private PointD? _clickPosition;

    public TextTool(IToolHost host) : base(host) { }

    public override string Name => "Text";
    public override string IconKey => "Text";
    public override Key Shortcut => Key.T;
    public override object Options => _options;
    public override Cursor Cursor => Cursors.IBeam;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button == MouseButton.Left) _clickPosition = e.Position;
    }

    // The dialog opens on mouse-up so the canvas has released the mouse first.
    public override void MouseUp(ToolMouseArgs e)
    {
        if (_clickPosition is not PointD position) return;
        _clickPosition = null;

        // Clicking existing text edits it instead of adding new text.
        if (Host.FindLayerAt(position) is { Object: TextObject existing } layer)
        {
            Document.ActiveLayer = layer;
            EditExisting(existing);
            return;
        }
        PlaceText(position);
    }

    private void PlaceText(PointD position)
    {
        var text = Host.Dialogs.ShowTextInput("Add Text", "Type your text (Enter = new line):", "");
        if (string.IsNullOrWhiteSpace(text)) return;

        var obj = new TextObject(new ObjectBox(Math.Round(position.X), Math.Round(position.Y), 1, 1),
            text.Replace("\r\n", "\n"), _options.FontFamily.Source, _options.FontSize, Host.Colors.Foreground,
            _options.Bold, _options.Italic);
        var (w, h) = ObjectRendering.Renderer.MeasureText(obj);
        obj = obj with { Box = obj.Box with { Width = w, Height = h } };

        Host.AddObjectLayer(obj, "Add Text");
        Host.SelectTool<MoveTool>();
        Host.SetStatus("Text added. Drag to move, corners to resize; change font and color in the Properties panel.");
    }

    private void EditExisting(TextObject existing)
    {
        var text = Host.Dialogs.ShowTextInput("Edit Text", "Text (Enter = new line):", existing.Text);
        if (string.IsNullOrWhiteSpace(text) || text == existing.Text) return;
        var updated = existing with { Text = text.Replace("\r\n", "\n") };
        var (w, h) = ObjectRendering.Renderer.MeasureText(updated);
        updated = updated with { Box = updated.Box with { Width = w, Height = h } };
        if (Document.ActiveLayer is { } layer)
            Push(Core.History.ObjectChangeAction.Apply("Edit Text", layer, updated));
    }
}
