using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Project;

/// <summary>
/// Hands out file names for pictures while writing a project (the same picture always gets the same name, so a picture
/// used twice is stored once) and finds them again while reading.
/// </summary>
internal sealed class PixelStore
{
    private readonly Dictionary<PixelBuffer, string> _names = new(ReferenceEqualityComparer.Instance);
    private readonly Func<string, PixelBuffer>? _load;

    /// <summary>For writing.</summary>
    public PixelStore() { }

    /// <summary>For reading: <paramref name="load"/> returns the picture stored under a name.</summary>
    public PixelStore(Func<string, PixelBuffer> load) => _load = load;

    /// <summary>Pictures in the order they were named.</summary>
    public IEnumerable<KeyValuePair<PixelBuffer, string>> Written => _names;

    public string NameFor(PixelBuffer buffer)
    {
        if (!_names.TryGetValue(buffer, out var name))
        {
            name = ProjectFile.ImageFolder + _names.Count + ".png";
            _names.Add(buffer, name);
        }
        return name;
    }

    public PixelBuffer Load(string name)
        => _load?.Invoke(name) ?? throw new InvalidOperationException("This store can only write.");
}
