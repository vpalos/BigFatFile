using System.Text.Json;
using System.Text.Json.Serialization;

namespace BigFatFile.Contracts.Hashing;

public sealed class ContentHashJsonConverter : JsonConverter<ContentHash>
{
    public override ContentHash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        return ContentHash.TryParse(text, out var hash)
            ? hash
            : throw new JsonException($"'{text}' is not a content hash.");
    }

    public override void Write(Utf8JsonWriter writer, ContentHash value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
