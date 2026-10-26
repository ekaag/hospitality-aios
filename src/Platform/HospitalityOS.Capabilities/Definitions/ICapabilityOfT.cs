using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Definitions;

/// <summary>
/// A capability taking <typeparamref name="TInput"/> and returning <typeparamref name="TOutput"/>; both are published
/// as JSON Schema, so they are contract types (records in the wire format, objects at the top level). A constructor
/// parameter without a default value is required even when nullable (the caller must send <c>null</c>); give optional
/// members a default. <c>[Description]</c> on members becomes their schema description.
/// </summary>
/// <remarks>
/// Implementations are scoped services with constructor-injected ports. They change state only through repositories
/// and never commit: the executor commits their changes together with the invocation record, its events, and the
/// audit record, or discards them all when they fail. They run in their own scope, for the caller's tenant and flow.
/// </remarks>
/// <typeparam name="TInput">The input object.</typeparam>
/// <typeparam name="TOutput">The output object.</typeparam>
public interface ICapability<TInput, TOutput> : ICapability
    where TInput : class
    where TOutput : class
{
    /// <summary>
    /// Runs the capability on validated input. Expected failures (not found, invalid state, domain validation) are
    /// returned, not thrown; nothing the capability changed is saved when it fails.
    /// </summary>
    Task<Result<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken);
}
