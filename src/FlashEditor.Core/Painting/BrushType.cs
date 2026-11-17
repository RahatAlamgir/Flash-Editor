namespace FlashEditor.Core.Painting;

/// <summary>The shape / texture of the mark a brush leaves.</summary>
public enum BrushType
{
    /// <summary>Round, with a hardness setting (the classic brush).</summary>
    Round,
    /// <summary>A square tip.</summary>
    Square,
    /// <summary>A flat, slanted tip (like a calligraphy pen): thick strokes one way, thin the other.</summary>
    Calligraphy,
    /// <summary>Hard-edged and slightly grainy, like graphite.</summary>
    Pencil,
    /// <summary>Soft, rough and speckled, like chalk or charcoal.</summary>
    Chalk,
    /// <summary>A fine mist of tiny dots (airbrush / spray can).</summary>
    Spray,
    /// <summary>Random blobs of different sizes around the path.</summary>
    Splatter,
}

public static class BrushTypes
{
    public static string Description(BrushType type) => type switch
    {
        BrushType.Round => "Round brush with adjustable hardness",
        BrushType.Square => "Square tip",
        BrushType.Calligraphy => "Flat slanted tip: thick one way, thin the other",
        BrushType.Pencil => "Hard-edged, slightly grainy graphite",
        BrushType.Chalk => "Soft, rough, speckled chalk",
        BrushType.Spray => "Fine mist of dots (airbrush)",
        BrushType.Splatter => "Random blobs around the path",
        _ => type.ToString(),
    };

    /// <summary>Whether the Hardness setting changes how this type looks.</summary>
    public static bool UsesHardness(BrushType type) => type is BrushType.Round or BrushType.Square or BrushType.Calligraphy or BrushType.Chalk;
}
