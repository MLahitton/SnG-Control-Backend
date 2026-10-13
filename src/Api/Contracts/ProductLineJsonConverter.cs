using System.Text.Json;
using System.Text.Json.Serialization;
using SnG.Control.Domain.Enums;

namespace SnG.Control.Api.Contracts;

public class ProductLineJsonConverter : JsonConverter<ProductLine>
{
    private static readonly JsonConverter<ProductLine> NamedValues =
        (JsonConverter<ProductLine>)new JsonStringEnumConverter<ProductLine>(allowIntegerValues: false)
            .CreateConverter(typeof(ProductLine), JsonSerializerOptions.Default);

    public override ProductLine Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || reader.GetString()!.Contains(','))
        {
            throw new JsonException("ProductLine must be a single named value.");
        }

        return NamedValues.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, ProductLine value, JsonSerializerOptions options) =>
        NamedValues.Write(writer, value, options);
}
