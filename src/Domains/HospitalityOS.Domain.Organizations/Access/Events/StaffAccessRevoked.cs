using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Organizations.Access.Events;

/// <summary>
/// Someone's access ended: their sign-in and any open invite stopped working.
/// </summary>
public sealed record StaffAccessRevoked(
    StaffAccessId StaffAccessId,
    DateTimeOffset OccurredAt) : IDomainEvent;
