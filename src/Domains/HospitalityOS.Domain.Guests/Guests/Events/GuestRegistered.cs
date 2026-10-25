using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Guests.Events;

/// <summary>
/// A guest was registered. Carries no profile data, so personal data does not spread through the event log.
/// </summary>
public sealed record GuestRegistered(GuestId GuestId, DateTimeOffset OccurredAt) : IDomainEvent;
