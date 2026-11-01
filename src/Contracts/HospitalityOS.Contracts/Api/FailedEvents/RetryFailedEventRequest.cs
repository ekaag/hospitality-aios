namespace HospitalityOS.Contracts.Api.FailedEvents;

/// <summary>
/// <c>POST /api/v1/failed-events/{eventId}/retry</c>: processes a failed event again: publishes it again, or, with a
/// handler, delivers it again (handlers that already handled it skip it). If it fails again it is listed again.
/// </summary>
/// <param name="Handler">The handler whose failure to retry; omitted for a publish failure.</param>
public sealed record RetryFailedEventRequest(string? Handler = null);
