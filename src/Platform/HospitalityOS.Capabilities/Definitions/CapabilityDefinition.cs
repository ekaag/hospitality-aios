using System.Text.Json;
using System.Text.RegularExpressions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Capabilities.Definitions;

/// <summary>
/// A registered capability: its validated descriptor, its implementing type, and its input and output types with their
/// JSON Schemas (spec §11). Immutable.
/// </summary>
public sealed partial class CapabilityDefinition
{
    /// <summary>
    /// Longest allowed name. AI providers cap tool names at 64 characters.
    /// </summary>
    public const int MaxNameLength = 64;

    /// <summary>
    /// Longest allowed description.
    /// </summary>
    public const int MaxDescriptionLength = 1024;

    private CapabilityDefinition(CapabilityDescriptor descriptor, Type capabilityType, Type inputType, Type outputType, JsonElement inputSchema, JsonElement outputSchema)
    {
        Name = descriptor.Name;
        Version = descriptor.Version;
        Description = descriptor.Description;
        Authorization = descriptor.Authorization;
        ConfirmationPolicy = descriptor.ConfirmationPolicy;
        SideEffects = descriptor.SideEffects;
        Idempotency = descriptor.Idempotency;
        AuditClassification = descriptor.AuditClassification;
        CapabilityType = capabilityType;
        InputType = inputType;
        OutputType = outputType;
        InputSchema = inputSchema;
        OutputSchema = outputSchema;
    }

    /// <summary>
    /// <c>&lt;Area&gt;.&lt;Action&gt;</c>, e.g. <c>GuestService.CreateRequest</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The version, from 1.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// What it does and when to use it.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Who may invoke it.
    /// </summary>
    public CapabilityAuthorization Authorization { get; }

    /// <summary>
    /// Whether it runs only after explicit confirmation.
    /// </summary>
    public ConfirmationPolicy ConfirmationPolicy { get; }

    /// <summary>
    /// The most far-reaching effect it can have.
    /// </summary>
    public SideEffect SideEffects { get; }

    /// <summary>
    /// How a repeated invocation is kept from repeating its effect.
    /// </summary>
    public IdempotencyPolicy Idempotency { get; }

    /// <summary>
    /// How much of an invocation its audit record keeps.
    /// </summary>
    public AuditClassification AuditClassification { get; }

    /// <summary>
    /// The class implementing it.
    /// </summary>
    public Type CapabilityType { get; }

    /// <summary>
    /// The input type, read from the wire format.
    /// </summary>
    public Type InputType { get; }

    /// <summary>
    /// The output type, written in the wire format.
    /// </summary>
    public Type OutputType { get; }

    /// <summary>
    /// JSON Schema of the input: required and nullable members, enum names, IDs as UUID strings, and no members beyond
    /// those declared.
    /// </summary>
    public JsonElement InputSchema { get; }

    /// <summary>
    /// JSON Schema of the output.
    /// </summary>
    public JsonElement OutputSchema { get; }

    /// <summary>
    /// Builds and checks the definition of <typeparamref name="TCapability"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The declaration is invalid; the message lists every problem.</exception>
    public static CapabilityDefinition For<TCapability>()
        where TCapability : ICapability =>
        Create(typeof(TCapability), TCapability.Descriptor);

    /// <summary>
    /// Builds and checks the definition of <paramref name="capabilityType"/> declared by <paramref name="descriptor"/>.
    /// </summary>
    internal static CapabilityDefinition Create(Type capabilityType, CapabilityDescriptor descriptor)
    {
        Type[] signatures = capabilityType.GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICapability<,>))
            .ToArray();

        if (signatures.Length != 1)
        {
            throw new InvalidOperationException($"Capability {capabilityType.FullName} must implement ICapability<TInput, TOutput> exactly once.");
        }

        if (descriptor is null)
        {
            throw new InvalidOperationException($"Capability {capabilityType.FullName} has no descriptor.");
        }

        Type inputType = signatures[0].GetGenericArguments()[0];
        Type outputType = signatures[0].GetGenericArguments()[1];
        JsonElement inputSchema = CapabilitySchemas.For(inputType);
        JsonElement outputSchema = CapabilitySchemas.For(outputType);

        List<string> problems = [.. Check(descriptor)];
        problems.AddRange(CheckIsObject("input", inputType, inputSchema));
        problems.AddRange(CheckIsObject("output", outputType, outputSchema));

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Capability {capabilityType.FullName} is invalid:{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", problems)}");
        }

        return new CapabilityDefinition(descriptor, capabilityType, inputType, outputType, inputSchema, outputSchema);
    }

    /// <summary>
    /// The definition as <c>/api/v1/capabilities</c> publishes it.
    /// </summary>
    public CapabilityDescription Describe() =>
        new(Name, Version, Description, InputSchema, OutputSchema, Authorization, ConfirmationPolicy, SideEffects, Idempotency, AuditClassification);

    /// <inheritdoc />
    public override string ToString() => $"{Name} v{Version}";

    private static IEnumerable<string> Check(CapabilityDescriptor descriptor)
    {
        if (descriptor.Name is null || descriptor.Name.Length > MaxNameLength || !NamePattern().IsMatch(descriptor.Name))
        {
            yield return $"Name '{descriptor.Name}' must be <Area>.<Action> in PascalCase (letters and digits), at most {MaxNameLength} characters, e.g. GuestService.CreateRequest.";
        }

        if (descriptor.Version < 1)
        {
            yield return $"Version {descriptor.Version} must be 1 or more.";
        }

        if (string.IsNullOrWhiteSpace(descriptor.Description))
        {
            yield return "Description is required: AI agents choose capabilities by it.";
        }
        else if (descriptor.Description.Length > MaxDescriptionLength)
        {
            yield return $"Description must be at most {MaxDescriptionLength} characters.";
        }

        ValueList<CapabilityCaller>? callers = descriptor.Authorization?.AllowedCallers;

        if (callers is not { Count: > 0 })
        {
            yield return "Authorization must allow at least one caller.";
        }
        else if (callers.Any(caller => !Enum.IsDefined(caller)))
        {
            yield return "Authorization lists an unknown caller.";
        }
        else if (callers.Distinct().Count() != callers.Count)
        {
            yield return "Authorization lists a caller more than once.";
        }

        if (!Enum.IsDefined(descriptor.ConfirmationPolicy)
            || !Enum.IsDefined(descriptor.SideEffects)
            || !Enum.IsDefined(descriptor.Idempotency)
            || !Enum.IsDefined(descriptor.AuditClassification))
        {
            yield return "ConfirmationPolicy, SideEffects, Idempotency, and AuditClassification must be defined values.";
        }

        if (descriptor.SideEffects is SideEffect.None && descriptor.ConfirmationPolicy is ConfirmationPolicy.Required)
        {
            yield return "A read-only capability cannot require confirmation: there is nothing to confirm.";
        }

        if (descriptor.SideEffects is SideEffect.None && descriptor.Idempotency is IdempotencyPolicy.KeyRequired)
        {
            yield return "A read-only capability is inherently idempotent; it cannot require an idempotency key.";
        }

        if (descriptor.SideEffects is SideEffect.External && descriptor.Idempotency is not IdempotencyPolicy.KeyRequired)
        {
            yield return "A capability with external effects must require an idempotency key: a repeated message or call cannot be taken back (P8).";
        }
    }

    private static IEnumerable<string> CheckIsObject(string role, Type type, JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out JsonElement schemaType) || schemaType.ValueKind is not JsonValueKind.String || schemaType.GetString() != "object")
        {
            yield return $"The {role} {type.Name} must be written as a JSON object (a record or class), not {schema}.";
        }
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*\\.[A-Z][A-Za-z0-9]*$")]
    private static partial Regex NamePattern();
}
