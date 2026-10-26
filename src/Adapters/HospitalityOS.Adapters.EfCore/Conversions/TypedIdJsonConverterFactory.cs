using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Adapters.EfCore.Conversions;

/// <summary>
/// Writes strongly typed IDs inside JSON columns as plain GUID strings.
/// </summary>
internal sealed class TypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsValueType && typeof(ITypedId).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(TypedIdJsonConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class TypedIdJsonConverter<TId> : JsonConverter<TId>
        where TId : struct, ITypedId
    {
        public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => TypedIds<TId>.Create(reader.GetGuid());

        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);
    }
}
