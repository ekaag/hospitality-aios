using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Eventing.Failures;

/// <summary>
/// The events a tenant's processing gave up on, and processing them again (L1). System-level: callers authorize it.
/// </summary>
public interface IFailedEvents
{
    /// <summary>
    /// The tenant's failed events, newest first: those the outbox could not publish, and every handler's dead letters.
    /// </summary>
    Task<IReadOnlyList<FailedEvent>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Processes a failed event again: requeues it to be published or, for a handler's failure, takes it out of the
    /// handler's dead letters and publishes it again. Handlers that already handled it skip it (P8).
    /// </summary>
    /// <returns>Whether the tenant had that failure.</returns>
    Task<bool> RetryAsync(TenantId tenantId, Guid eventId, string? handler, CancellationToken cancellationToken);
}

/// <summary>
/// One failed event.
/// </summary>
/// <param name="Message">The event.</param>
/// <param name="Handler">The handler that failed, or <c>null</c> when publishing failed.</param>
/// <param name="Error">The last failure.</param>
/// <param name="Attempts">How often it was tried.</param>
/// <param name="FailedAt">When it was given up on, where the store knows.</param>
public sealed record FailedEvent(EventMessage Message, string? Handler, string Error, int Attempts, DateTimeOffset? FailedAt);
