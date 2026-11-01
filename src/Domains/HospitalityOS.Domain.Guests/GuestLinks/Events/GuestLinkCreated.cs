using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.GuestLinks.Events;

/// <summary>
/// Staff created a check-in link for a stay.
/// </summary>
public sealed record GuestLinkCreated(
    GuestLinkId GuestLinkId,
    StayContextId StayContextId,
    PropertyId PropertyId,
    DateTimeOffset OccurredAt) : IDomainEvent;
