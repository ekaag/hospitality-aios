using System.Text.Json;
using System.Text.Json.Serialization;

namespace HospitalityOS.Adapters.EfCore.Conversions;

/// <summary>
/// How value objects are written to JSON columns: camelCase, enums as strings, IDs as plain GUIDs, and only
/// state (computed properties are left out). Records are read back through their constructors, so the domain
/// needs nothing for this. Changing these options changes stored data: keep them stable.
/// </summary>
internal static class PersistenceJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"Stored {typeof(T).Name} is null.");

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            IgnoreReadOnlyProperties = true,
            Converters =
            {
                new JsonStringEnumConverter(),
                new TypedIdJsonConverterFactory(),
                new ValueListJsonConverterFactory(),
            },
        };
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
