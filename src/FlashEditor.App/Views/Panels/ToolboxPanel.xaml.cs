using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlashEditor.App.Tools;
using FlashEditor.App.ViewModels;

namespace FlashEditor.App.Views.Panels;

/// <summary>Left vertical toolbox with the tools (similar tools share a button with a flyout list) and the color swatches.</summary>
public partial class ToolboxPanel : UserControl
{
    public ToolboxPanel() => InitializeComponent();

    private static ToolGroup? GroupOf(object sender) => (sender as FrameworkElement)?.DataContext as ToolGroup;

    /// <summary>Click on the button: starts the tool the button shows.</summary>
    private void OnGroupClick(object sender, MouseButtonEventArgs e)
    {
        if (GroupOf(sender) is { } group) group.ActivateCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>Right click: opens the list of tools of the group.</summary>
    private void OnGroupRightClick(object sender, MouseButtonEventArgs e)
    {
        if (GroupOf(sender) is { HasMore: true } group) group.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>Click on the corner triangle: opens the list of tools of the group.</summary>
    private void OnGroupTriangleClick(object sender, MouseButtonEventArgs e)
    {
        if (GroupOf(sender) is { } group) group.IsOpen = !group.IsOpen;
        e.Handled = true;
    }

    /// <summary>A tool of the list was chosen: it becomes the one the button shows, and starts.</summary>
    private void OnPickTool(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ToolBase tool } && DataContext is MainViewModel main)
        {
            var group = main.Tools.Groups.FirstOrDefault(g => g.Contains(tool));
            group?.PickCommand.Execute(tool);
        }
        e.Handled = true;
    }
}
