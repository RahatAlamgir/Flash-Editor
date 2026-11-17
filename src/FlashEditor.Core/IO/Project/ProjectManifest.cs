using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.IO.Project;

/// <summary>The text part of a project file (project.json). Pictures are stored as separate PNG files and referred to by name.</summary>
internal sealed class ProjectManifest
{
    public string Format { get; set; } = ProjectFile.FormatName;
    public int Version { get; set; } = ProjectFile.CurrentVersion;
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Index of the layer that was selected (bottom layer = 0).</summary>
    public int ActiveLayer { get; set; }

    /// <summary>Bottom to top.</summary>
    public List<LayerEntry> Layers { get; set; } = new();
}

internal sealed class LayerEntry
{
    public string Name { get; set; } = "Layer";
    public double Opacity { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }

    /// <summary>What is on the canvas (also stored for object layers, so opening never depends on how objects are drawn).</summary>
    public PixelBuffer? Pixels { get; set; }

    /// <summary>The larger picture that keeps parts moved past the canvas edge, and where it starts.</summary>
    public PixelBuffer? Extended { get; set; }
    public int ExtendedX { get; set; }
    public int ExtendedY { get; set; }

    /// <summary>The picture from before the background was removed (for the Refine brush).</summary>
    public PixelBuffer? Restore { get; set; }

    /// <summary>The editable shape, text, picture or group, or null for a normal pixel layer.</summary>
    public LayerObject? Object { get; set; }
}
