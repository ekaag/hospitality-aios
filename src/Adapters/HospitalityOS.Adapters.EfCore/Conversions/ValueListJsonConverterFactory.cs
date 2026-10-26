using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Adapters.EfCore.Conversions;

/// <summary>
/// Writes <see cref="ValueList{T}"/> as a JSON array and reads it back.
/// </summary>
internal sealed class ValueListJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ValueList<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(ValueListJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class ValueListJsonConverter<T> : JsonConverter<ValueList<T>>
    {
        public override ValueList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ValueList.From(JsonSerializer.Deserialize<T[]>(ref reader, options) ?? []);

        public override void Write(Utf8JsonWriter writer, ValueList<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.AsEnumerable(), options);
    }
}
