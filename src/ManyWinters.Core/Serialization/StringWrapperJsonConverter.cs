using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManyWinters.Core.Serialization;

public abstract class StringWrapperJsonConverter<T> : JsonConverter<T>
{
    protected abstract T Create(string value);

    protected abstract string GetValue(T instance);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string for {typeToConvert.Name}.");
        }

        return Create(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(GetValue(value));
}
