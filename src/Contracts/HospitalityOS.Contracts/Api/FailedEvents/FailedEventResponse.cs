namespace HospitalityOS.Contracts.Api.FailedEvents;

/// <summary>
/// An event the platform gave up processing (spec §37, §42: failed event processing is visible): one the outbox could not
/// publish, or one a handler kept failing on after every redelivery.
/// </summary>
/// <param name="EventId">The event.</param>
/// <param name="EventType">Its type, e.g. <c>HumanActionCompleted</c>.</param>
/// <param name="Stage">Whether publishing or handling failed.</param>
/// <param name="Handler">For a handling failure, the handler that failed, e.g. <c>guest-service.human-action-completed</c>.</param>
/// <param name="Error">The last failure, as the platform recorded it.</param>
/// <param name="Attempts">How often it was tried.</param>
/// <param name="AggregateType">What changed, e.g. <c>HumanAction</c>.</param>
/// <param name="AggregateId">Which one.</param>
/// <param name="CorrelationId">The flow it belongs to, to find it in AI traces and logs.</param>
/// <param name="RecordedAt">When the event happened.</param>
/// <param name="FailedAt">When it was given up on, where known.</param>
public sealed record FailedEventResponse(
    Guid EventId,
    string EventType,
    FailedEventStage Stage,
    string? Handler,
    string Error,
    int Attempts,
    string AggregateType,
    Guid AggregateId,
    Guid CorrelationId,
    DateTimeOffset RecordedAt,
    DateTimeOffset? FailedAt);
