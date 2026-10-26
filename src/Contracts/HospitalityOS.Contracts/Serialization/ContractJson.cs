using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Serialization;

/// <summary>
/// The platform's JSON wire format for events, capability inputs and outputs, and APIs: camelCase, enums as names, IDs
/// as plain GUID strings, lists as arrays (coding standards §1), and strict reading: a missing required value or a
/// null where none is allowed is rejected, never defaulted. Changing these options changes every published event and
/// capability schema: keep them stable. Numbers must be JSON numbers, not strings.
/// </summary>
public static class ContractJson
{
    /// <summary>
    /// Read-only options in the wire format.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateReadOnly();

    /// <summary>
    /// Applies the wire format to <paramref name="options"/>, e.g. a host's HTTP JSON options. Call before the options
    /// are first used.
    /// </summary>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new TypedIdConverterFactory());
        options.Converters.Add(new ValueListConverterFactory());
    }

    private static JsonSerializerOptions CreateReadOnly()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }

    private sealed class TypedIdConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsValueType && typeof(ITypedId).IsAssignableFrom(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(TypedIdConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class TypedIdConverter<TId> : JsonConverter<TId>
        where TId : struct, ITypedId
    {
        private static readonly Func<Guid, TId> create = CompileConstructor();

        public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => create(reader.GetGuid());

        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) => writer.WriteStringValue(value.Value);

        private static Func<Guid, TId> CompileConstructor()
        {
            ParameterExpression value = Expression.Parameter(typeof(Guid), "value");
            System.Reflection.ConstructorInfo constructor = typeof(TId).GetConstructor([typeof(Guid)])
                ?? throw new InvalidOperationException($"{typeof(TId).Name} needs a public constructor taking a Guid.");

            return Expression.Lambda<Func<Guid, TId>>(Expression.New(constructor, value), value).Compile();
        }
    }

    private sealed class ValueListConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ValueList<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(ValueListConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
    }

    private sealed class ValueListConverter<T> : JsonConverter<ValueList<T>>
    {
        public override ValueList<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ValueList.From(JsonSerializer.Deserialize<T[]>(ref reader, options) ?? []);

        public override void Write(Utf8JsonWriter writer, ValueList<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.AsEnumerable(), options);
    }
}
