namespace HospitalityOS.Contracts.Api.FailedEvents;

/// <summary>
/// A failed event sent to be processed again.
/// </summary>
/// <param name="EventId">The event.</param>
/// <param name="Stage">The failure retried.</param>
/// <param name="Handler">For a handling failure, the handler.</param>
public sealed record FailedEventRetriedResponse(Guid EventId, FailedEventStage Stage, string? Handler);
