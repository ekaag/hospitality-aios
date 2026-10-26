namespace HospitalityOS.Capabilities.Definitions;

/// <summary>
/// A capability taking <typeparamref name="TInput"/> and returning <typeparamref name="TOutput"/>; both are published
/// as JSON Schema, so they are contract types (records in the wire format, objects at the top level). A constructor
/// parameter without a default value is required even when nullable (the caller must send <c>null</c>); give optional
/// members a default. <c>[Description]</c> on members becomes their schema description. The executor (slice D2) adds
/// how it runs.
/// </summary>
/// <typeparam name="TInput">The input object.</typeparam>
/// <typeparam name="TOutput">The output object.</typeparam>
public interface ICapability<TInput, TOutput> : ICapability
    where TInput : class
    where TOutput : class;
