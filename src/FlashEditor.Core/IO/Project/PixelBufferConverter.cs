using System.Text.Json;
using System.Text.Json.Serialization;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Project;

/// <summary>Writes a picture as the name of its PNG file inside the project, and reads it back from there.</summary>
internal sealed class PixelBufferConverter : JsonConverter<PixelBuffer>
{
    private readonly PixelStore _store;

    public PixelBufferConverter(PixelStore store) => _store = store;

    public override PixelBuffer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => _store.Load(reader.GetString() ?? throw new JsonException("A picture has no file name."));

    public override void Write(Utf8JsonWriter writer, PixelBuffer value, JsonSerializerOptions options)
        => writer.WriteStringValue(_store.NameFor(value));
}
