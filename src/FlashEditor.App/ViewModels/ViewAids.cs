using System.IO;
using System.Text.Json;
using FlashEditor.Core.Common;
using FlashEditor.Core.Snapping;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// The helpers for placing things exactly: rulers, grid, guide lines and snapping. They are switched on and off with the toggle
/// buttons above the canvas, and remembered between runs.
/// </summary>
public sealed class ViewAids : ObservableObject
{
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings", "view-aids.json");

    private sealed record Stored(bool ShowRulers, bool ShowGrid, bool ShowGuides, bool SnapEnabled,
        bool SnapToObjects, bool SnapToGrid, bool SnapToCanvas, bool SnapToGuides, double GridSize, bool SnapToSpacing = true);

    private bool _showRulers, _showGrid, _showGuides = true;
    private bool _snapToSpacing = true;
    private bool _snapEnabled = true, _snapToObjects = true, _snapToGrid = true, _snapToCanvas = true, _snapToGuides = true;
    private double _gridSize = 50;
    private bool _loading;

    public static IReadOnlyList<double> GridSizes { get; } = new double[] { 5, 10, 20, 25, 40, 50, 100, 200 };

    public bool ShowRulers { get => _showRulers; set { if (SetField(ref _showRulers, value)) Save(); } }
    public bool ShowGrid { get => _showGrid; set { if (SetField(ref _showGrid, value)) Save(); } }
    public bool ShowGuides { get => _showGuides; set { if (SetField(ref _showGuides, value)) Save(); } }

    /// <summary>The master switch: off = nothing snaps.</summary>
    public bool SnapEnabled { get => _snapEnabled; set { if (SetField(ref _snapEnabled, value)) Save(); } }
    public bool SnapToObjects { get => _snapToObjects; set { if (SetField(ref _snapToObjects, value)) Save(); } }
    public bool SnapToGrid { get => _snapToGrid; set { if (SetField(ref _snapToGrid, value)) Save(); } }
    public bool SnapToCanvas { get => _snapToCanvas; set { if (SetField(ref _snapToCanvas, value)) Save(); } }
    public bool SnapToGuides { get => _snapToGuides; set { if (SetField(ref _snapToGuides, value)) Save(); } }

    /// <summary>Equal gaps: snap so the distance to a neighbor equals the distance between other objects (the gaps are shown in pixels).</summary>
    public bool SnapToSpacing { get => _snapToSpacing; set { if (SetField(ref _snapToSpacing, value)) Save(); } }

    /// <summary>Distance between grid lines, in pixels.</summary>
    public double GridSize { get => _gridSize; set { if (SetField(ref _gridSize, Math.Clamp(Math.Round(value), 2, 1000))) Save(); } }

    /// <summary>What to snap to right now. Grid and guides only count while they are shown (you snap to what you can see).</summary>
    public SnapSettings ToSnapSettings()
        => new(_snapToObjects, _snapToGrid && _showGrid, _snapToCanvas, _snapToGuides && _showGuides, _gridSize, _snapToSpacing);

    public static ViewAids Load()
    {
        var aids = new ViewAids();
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath)) is { } s)
            {
                aids._loading = true;
                aids.ShowRulers = s.ShowRulers; aids.ShowGrid = s.ShowGrid; aids.ShowGuides = s.ShowGuides; aids.SnapEnabled = s.SnapEnabled;
                aids.SnapToObjects = s.SnapToObjects; aids.SnapToGrid = s.SnapToGrid; aids.SnapToCanvas = s.SnapToCanvas;
                aids.SnapToGuides = s.SnapToGuides; aids.GridSize = s.GridSize; aids.SnapToSpacing = s.SnapToSpacing;
                aids._loading = false;
            }
        }
        catch (Exception)
        {
            aids._loading = false; // a damaged file: start with the defaults
        }
        return aids;
    }

    private void Save()
    {
        if (_loading) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Stored(_showRulers, _showGrid, _showGuides, _snapEnabled,
                _snapToObjects, _snapToGrid, _snapToCanvas, _snapToGuides, _gridSize, _snapToSpacing)));
        }
        catch (Exception)
        {
            // The folder cannot be written: the settings just are not remembered.
        }
    }
}
