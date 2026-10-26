using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Activities;

/// <summary>
/// Version 1 of <c>PropertyEventCreated</c>: an event or activity was scheduled.
/// </summary>
/// <param name="PropertyEventId">The property event (activity).</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record PropertyEventCreatedV1(
    Guid PropertyEventId,
    PropertyId PropertyId,
    PropertyEventDetailsData Details,
    string Status) : IEventPayload;
