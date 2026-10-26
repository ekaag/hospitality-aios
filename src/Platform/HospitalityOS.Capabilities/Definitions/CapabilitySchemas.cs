using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Capabilities.Definitions;

/// <summary>
/// Generates a type's JSON Schema from <see cref="CapabilityJson.Options"/>, so the schema always describes what the
/// serializer reads and writes. The exporter cannot see inside custom converters, so typed IDs (UUID strings) and
/// <see cref="ValueList{T}"/> (arrays) are described here; <see cref="DescriptionAttribute"/> on members and types
/// becomes <c>description</c>, which agents read.
/// </summary>
internal static class CapabilitySchemas
{
    private static readonly JsonSchemaExporterOptions exporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = Transform,
    };

    public static JsonElement For(Type type) => JsonSerializer.SerializeToElement(Node(type));

    private static JsonNode Node(Type type) => CapabilityJson.Options.GetJsonSchemaAsNode(type, exporterOptions);

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        Type type = Nullable.GetUnderlyingType(context.TypeInfo.Type) ?? context.TypeInfo.Type;
        bool nullable = type != context.TypeInfo.Type || context.PropertyInfo is { IsGetNullable: true } && !type.IsValueType;

        if (typeof(ITypedId).IsAssignableFrom(type))
        {
            schema = new JsonObject { ["type"] = TypeKeyword("string", nullable), ["format"] = "uuid" };
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueList<>))
        {
            schema = new JsonObject { ["type"] = TypeKeyword("array", nullable), ["items"] = Node(type.GetGenericArguments()[0]) };
        }

        string? description = DescriptionOf(context.PropertyInfo?.AttributeProvider)
            ?? DescriptionOf(context.PropertyInfo?.AssociatedParameter?.AttributeProvider)
            ?? DescriptionOf(type);

        if (description is not null && schema is JsonObject described && !described.ContainsKey("description"))
        {
            described.Insert(0, "description", description);
        }

        return schema;
    }

    private static JsonNode TypeKeyword(string type, bool nullable) => nullable ? new JsonArray(type, "null") : JsonValue.Create(type);

    private static string? DescriptionOf(ICustomAttributeProvider? provider) =>
        provider?.GetCustomAttributes(typeof(DescriptionAttribute), inherit: true).OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
}
