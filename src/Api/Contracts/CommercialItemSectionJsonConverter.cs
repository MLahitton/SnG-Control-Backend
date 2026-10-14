using System.Text.Json;
using System.Text.Json.Serialization;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Api.Contracts;

public sealed class CommercialItemSectionJsonConverter : JsonConverter<CommercialItemSection>
{
    public override CommercialItemSection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Section must be Main or Additional.");
        var text = reader.GetString();
        if (string.Equals(text, "Main", StringComparison.OrdinalIgnoreCase)) return CommercialItemSection.Main;
        if (string.Equals(text, "Additional", StringComparison.OrdinalIgnoreCase)) return CommercialItemSection.Additional;
        throw new JsonException("Section must be Main or Additional.");
    }
    public override void Write(Utf8JsonWriter writer, CommercialItemSection value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
