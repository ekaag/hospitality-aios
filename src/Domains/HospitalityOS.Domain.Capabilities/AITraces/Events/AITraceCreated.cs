using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Capabilities.AITraces.Events;

/// <summary>
/// An AI turn was recorded.
/// </summary>
public sealed record AITraceCreated(
    AITraceId AITraceId,
    PropertyId? PropertyId,
    string? Agent,
    string TriggerType,
    Guid? TriggerId,
    AITraceOutcome Outcome,
    DateTimeOffset OccurredAt) : IDomainEvent;
