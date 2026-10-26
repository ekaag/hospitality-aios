using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Activities;

/// <summary>
/// Version 1 of <c>PropertyEventUpdated</c>: an event or activity changed or was cancelled.
/// </summary>
/// <param name="PropertyEventId">The property event (activity).</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record PropertyEventUpdatedV1(
    Guid PropertyEventId,
    PropertyEventDetailsData Details,
    string Status) : IEventPayload;
