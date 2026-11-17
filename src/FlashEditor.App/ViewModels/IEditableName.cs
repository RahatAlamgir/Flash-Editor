namespace FlashEditor.App.ViewModels;

/// <summary>A row in the Layers panel whose name can be edited in place (a layer or a part of a group).</summary>
public interface IEditableName
{
    string Name { get; set; }

    /// <summary>True while the name box is shown instead of the name text.</summary>
    bool IsEditingName { get; set; }
}
