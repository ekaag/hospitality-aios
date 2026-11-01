using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestLinks.Events;

/// <summary>
/// A stay's check-in link stopped working: staff revoked or replaced it.
/// </summary>
public sealed record GuestLinkRevoked(
    GuestLinkId GuestLinkId,
    StayContextId StayContextId,
    DateTimeOffset OccurredAt) : IDomainEvent;
