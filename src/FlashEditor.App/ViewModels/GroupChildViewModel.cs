using System.Windows.Media.Imaging;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// One part of a group, shown indented under the group's row in the Layers panel (like the layer list
/// in Canva). The name can be changed (undoable); the parts themselves are edited by ungrouping.
/// </summary>
public sealed class GroupChildViewModel : ObservableObject, IEditableName
{
    private const int ThumbnailSize = 64; // longest side of the preview, in pixels

    private readonly Layer _groupLayer;
    private readonly HistoryManager _history;
    private readonly int _index; // position in GroupObject.Members (bottom to top)
    private readonly GroupMember _member;
    private BitmapSource? _thumbnail;
    private bool _isEditingName;
    private bool _isSelected;

    public GroupChildViewModel(Layer groupLayer, HistoryManager history, int index, GroupMember member)
    {
        _groupLayer = groupLayer;
        _history = history;
        _index = index;
        _member = member;
    }

    /// <summary>The group's layer this part belongs to.</summary>
    public Layer GroupLayer => _groupLayer;

    /// <summary>Position of the part in the group (0 = bottom).</summary>
    public int Index => _index;

    /// <summary>True while this part is the one being edited inside its group (highlighted row).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string Name
    {
        get => _member.Name;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? _member.Name : value.Trim();
            if (value == _member.Name || _groupLayer.IsLocked) return;
            if (_groupLayer.Object is not GroupObject group || _index >= group.Members.Count) return;

            var members = group.Members.ToList();
            members[_index] = members[_index] with { Name = value };
            var step = ObjectChangeAction.Apply("Rename", _groupLayer, group with { Members = members });
            if (step != null) _history.Push(step);
        }
    }

    public bool IsEditingName
    {
        get => _isEditingName;
        set => SetField(ref _isEditingName, value);
    }

    /// <summary>Small picture of this part on its own (drawn the first time the row is shown).</summary>
    public BitmapSource Thumbnail => _thumbnail ??= CreateThumbnail(_member.Object);

    private static BitmapSource CreateThumbnail(LayerObject obj)
    {
        double longest = Math.Max(obj.Box.Width, obj.Box.Height);
        double factor = longest > ThumbnailSize ? ThumbnailSize / longest : 1;
        var scaled = ObjectTransforms.Scale(obj, factor, factor);

        // Draw it alone on a small canvas just big enough for it (borders and rotation included).
        var bounds = scaled.VisualBounds(4 + (scaled.Shadow?.ExtentFor(scaled.Box.Width, scaled.Box.Height) ?? 0) + (scaled.ThreeD?.Extent ?? 0)); // room for borders, the shadow and the 3D side
        int width = Math.Max(1, bounds.Width), height = Math.Max(1, bounds.Height);
        var placed = ObjectTransforms.Offset(scaled, -bounds.X, -bounds.Y);
        var pixels = ObjectRendering.Renderer.Render(placed, width, height);
        return BitmapConverter.ToFrozenBitmap(pixels);
    }
}
