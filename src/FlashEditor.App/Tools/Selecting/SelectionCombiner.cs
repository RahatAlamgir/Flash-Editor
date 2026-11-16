using System.Windows.Input;
using FlashEditor.Core.Documents;
using Selection = FlashEditor.Core.Selections.Selection;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>Shared rule for all selection tools: Shift adds to the selection, Alt subtracts from it.</summary>
public static class SelectionCombiner
{
    public static Selection.CombineMode ModeFor(ModifierKeys modifiers)
        => modifiers.HasFlag(ModifierKeys.Shift) ? Selection.CombineMode.Add
         : modifiers.HasFlag(ModifierKeys.Alt) ? Selection.CombineMode.Subtract
         : Selection.CombineMode.Replace;

    /// <summary>Applies a newly drawn selection to the document and returns a status message.</summary>
    public static string Apply(Document document, Selection? drawn, ModifierKeys modifiers)
    {
        var mode = ModeFor(modifiers);
        document.Selection = Selection.Combine(document.Selection, drawn, mode);
        if (document.Selection == null) return "Selection cleared";
        var b = document.Selection.Bounds;
        string verb = mode switch
        {
            Selection.CombineMode.Add => "Added to selection",
            Selection.CombineMode.Subtract => "Removed from selection",
            _ => "Selected",
        };
        return $"{verb} ({b.Width} x {b.Height} px). Shift = add, Alt = subtract.";
    }
}
