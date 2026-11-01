using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Guests;

/// <summary>
/// Version 1 of <c>GuestLinkRevoked</c>: a stay's check-in link stopped working.
/// </summary>
/// <param name="GuestLinkId">The link.</param>
/// <param name="StayContextId">The stay it opened.</param>
public sealed record GuestLinkRevokedV1(
    Guid GuestLinkId,
    StayContextId StayContextId) : IEventPayload;
