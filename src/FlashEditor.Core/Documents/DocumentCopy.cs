namespace FlashEditor.Core.Documents;

/// <summary>Makes an independent copy of a document (used to duplicate a page).</summary>
public static class DocumentCopy
{
    /// <summary>A new document with copies of all layers (objects stay editable). History and selection are not copied.</summary>
    public static Document Copy(Document source)
    {
        var copy = new Document(source.Width, source.Height);
        var layers = new List<Layer>();
        Layer? active = null;
        foreach (var layer in source.Layers)
        {
            var duplicate = layer.Duplicate(layer.Name);
            duplicate.IsLocked = layer.IsLocked;
            layers.Add(duplicate);
            if (layer == source.ActiveLayer) active = duplicate;
        }
        copy.SetLayers(layers, active);
        return copy;
    }
}
