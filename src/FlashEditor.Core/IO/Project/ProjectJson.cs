using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.IO.Project;

/// <summary>The JSON settings of project files. Objects are plain records, so they are stored by their own properties.</summary>
internal static class ProjectJson
{
    public static JsonSerializerOptions Create(PixelStore store) => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Customize } },
        Converters = { new PixelBufferConverter(store) },
    };

    private static void Customize(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object) return;

        // Properties that are only calculated from others (KindName, Center, HasCorners...) are not data.
        for (int i = info.Properties.Count - 1; i >= 0; i--)
            if (info.Properties[i].Set == null) info.Properties.RemoveAt(i);

        if (info.Type == typeof(LayerObject))
        {
            info.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "kind",
                DerivedTypes =
                {
                    new JsonDerivedType(typeof(ShapeObject), "shape"),
                    new JsonDerivedType(typeof(TextObject), "text"),
                    new JsonDerivedType(typeof(ImageObject), "image"),
                    new JsonDerivedType(typeof(GroupObject), "group"),
                    new JsonDerivedType(typeof(QrCodeObject), "qr"),
                    new JsonDerivedType(typeof(ChartObject), "chart"),
                    new JsonDerivedType(typeof(BarcodeObject), "barcode"),
                },
            };
        }
    }
}
