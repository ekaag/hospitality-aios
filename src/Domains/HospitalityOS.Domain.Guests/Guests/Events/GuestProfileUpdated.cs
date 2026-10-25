using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Guests.Guests.Events;

/// <summary>
/// A guest's profile changed. Carries no profile data (read the guest if needed), keeping personal data out of the event log.
/// </summary>
public sealed record GuestProfileUpdated(GuestId GuestId, DateTimeOffset OccurredAt) : IDomainEvent;
