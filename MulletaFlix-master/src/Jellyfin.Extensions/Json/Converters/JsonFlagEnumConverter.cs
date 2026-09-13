using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MulletaFlix.Extensions.Json.Converters;

/// <summary>
/// Enum flag to json array converter.
/// </summary>
/// <typeparam name="T">The type of enum.</typeparam>
public class JsonFlagEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private static readonly T[] _enumValues = Enum.GetValues<T>();

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected an array of {typeToConvert.Name} values.");
        }

        ulong rawValue = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string values in the {typeToConvert.Name} flags array.");
            }

            var name = reader.GetString();
            if (!Enum.TryParse<T>(name, ignoreCase: true, out var enumValue)
                || !Enum.IsDefined(typeof(T), enumValue))
            {
                throw new JsonException($"Unknown {typeToConvert.Name} flag '{name}'.");
            }

            rawValue |= Convert.ToUInt64(enumValue, CultureInfo.InvariantCulture);
        }

        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException($"The {typeToConvert.Name} flags array was not terminated.");
        }

        var underlyingType = Enum.GetUnderlyingType(typeToConvert);
        var convertedValue = Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
        return (T)Enum.ToObject(typeToConvert, convertedValue!);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var enumValue in _enumValues)
        {
            if (value.HasFlag(enumValue))
            {
                writer.WriteStringValue(enumValue.ToString());
            }
        }

        writer.WriteEndArray();
    }
}
