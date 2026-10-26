using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Services;

/// <summary>
/// Version 1 of <c>ServiceRequestCreated</c>: a guest or staff member requested a service (Journeys 9-11).
/// </summary>
/// <param name="ServiceRequestId">The service request.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="ServiceId">The service.</param>
/// <param name="ServiceCategory">The service's category (domain enum name).</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="Origin">Who or what asked for it.</param>
/// <param name="DueAt">Internal target time (UTC); never quoted to guests as an ETA.</param>
public sealed record ServiceRequestCreatedV1(
    ServiceRequestId ServiceRequestId,
    PropertyId PropertyId,
    Guid ServiceId,
    string ServiceCategory,
    ServiceRequestDetailsData Details,
    RequestOriginData Origin,
    DateTimeOffset? DueAt) : IEventPayload;
