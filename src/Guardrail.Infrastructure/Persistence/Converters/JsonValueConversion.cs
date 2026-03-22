using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Guardrail.Infrastructure.Persistence.Converters;

internal static class JsonValueConversion
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static JsonValueConversion()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static ValueConverter<T, string> CreateConverter<T>()
        => new(
            value => JsonSerializer.Serialize(value, SerializerOptions),
            value => string.IsNullOrWhiteSpace(value)
                ? CreateDefault<T>()
                : JsonSerializer.Deserialize<T>(value, SerializerOptions) ?? CreateDefault<T>());

    public static ValueComparer<T> CreateComparer<T>()
        => new(
            (left, right) => JsonSerializer.Serialize(left, SerializerOptions) == JsonSerializer.Serialize(right, SerializerOptions),
            value => JsonSerializer.Serialize(value, SerializerOptions).GetHashCode(),
            value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, SerializerOptions), SerializerOptions) ?? CreateDefault<T>());

    private static T CreateDefault<T>()
        => Activator.CreateInstance<T>();
}
