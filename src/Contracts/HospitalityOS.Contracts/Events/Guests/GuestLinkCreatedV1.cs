using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestLinkCreated</c>: staff created a check-in link for a stay.
/// </summary>
/// <param name="GuestLinkId">The link.</param>
/// <param name="StayContextId">The stay it opens.</param>
/// <param name="PropertyId">The property.</param>
public sealed record GuestLinkCreatedV1(
    Guid GuestLinkId,
    StayContextId StayContextId,
    PropertyId PropertyId) : IEventPayload;
