using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Ports.Scheduling;

/// <summary>
/// Work to run later (spec §2.2 <c>IScheduler</c>), e.g. checking whether assigned work was accepted in time.
/// </summary>
/// <param name="MessageId">Unique; a handler runs each message at most once (P8).</param>
/// <param name="Handler">The registered scheduled handler that runs it.</param>
/// <param name="TenantId">The tenant it runs in.</param>
/// <param name="CorrelationId">The flow it belongs to.</param>
/// <param name="CausationId">What scheduled it, e.g. an event ID.</param>
/// <param name="Payload">The handler's input, as contract JSON.</param>
/// <param name="DueAt">When to run it (UTC); at once when already past.</param>
public sealed record ScheduledMessage(
    Guid MessageId,
    string Handler,
    TenantId TenantId,
    Guid CorrelationId,
    Guid? CausationId,
    string Payload,
    DateTimeOffset DueAt);
