namespace HospitalityOS.Domain.Capabilities.CapabilityInvocations;

/// <summary>
/// Persistence port for <see cref="CapabilityInvocation"/>. Implementations are scoped to the current tenant (P10) and
/// commit through the unit of work.
/// </summary>
public interface ICapabilityInvocationRepository
{
    /// <summary>
    /// Loads an invocation, or returns <c>null</c> when it does not exist in the current tenant.
    /// </summary>
    Task<CapabilityInvocation?> GetByIdAsync(CapabilityInvocationId id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the succeeded invocation of <paramref name="capabilityName"/> (any version) with
    /// <paramref name="idempotencyKey"/> (exact match), or returns <c>null</c>; a repeated key returns its result instead
    /// of running again (P8). Backed by a unique index in persistence, so two racing invocations cannot both succeed.
    /// </summary>
    Task<CapabilityInvocation?> FindSucceededAsync(string capabilityName, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a new invocation, persisted when the unit of work commits.
    /// </summary>
    Task AddAsync(CapabilityInvocation invocation, CancellationToken cancellationToken);
}
