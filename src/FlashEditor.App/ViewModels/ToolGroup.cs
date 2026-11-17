using System.Windows.Input;
using FlashEditor.App.Tools;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// One button of the toolbox that holds several similar tools (like Photoshop): it shows the tool used last; the small triangle in
/// its corner opens the list of all tools of the group.
/// </summary>
public sealed class ToolGroup : ObservableObject
{
    private ToolBase _current;
    private bool _isActive;
    private bool _isOpen;

    public ToolGroup(IReadOnlyList<ToolBase> tools, Action<ToolBase> activate)
    {
        Tools = tools;
        _current = tools[0];
        PickCommand = new RelayCommand(p => { if (p is ToolBase t) { IsOpen = false; activate(t); } });
        ActivateCommand = new RelayCommand(() => activate(_current));
    }

    public IReadOnlyList<ToolBase> Tools { get; }

    /// <summary>The tool the button shows and starts.</summary>
    public ToolBase Current => _current;

    /// <summary>True when the group has more than one tool (then the button has the triangle).</summary>
    public bool HasMore => Tools.Count > 1;

    /// <summary>True while one of the group's tools is the active tool.</summary>
    public bool IsActive { get => _isActive; private set => SetField(ref _isActive, value); }

    /// <summary>True while the list of tools of the group is open.</summary>
    public bool IsOpen { get => _isOpen; set => SetField(ref _isOpen, value); }

    public string ToolTip => HasMore ? $"{Current.ToolTip}  -  click the corner triangle for more tools" : Current.ToolTip;

    public ICommand PickCommand { get; }
    public ICommand ActivateCommand { get; }

    public bool Contains(ToolBase tool) => Tools.Contains(tool);

    /// <summary>Called when the active tool changed (by a click, a shortcut or code): the group follows.</summary>
    public void OnActiveToolChanged(ToolBase active)
    {
        bool mine = Contains(active);
        if (mine && !ReferenceEquals(_current, active))
        {
            _current = active;
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(ToolTip));
        }
        IsActive = mine;
    }
}
