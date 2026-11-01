using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Organizations.Access.Events;

/// <summary>
/// Someone redeemed their invite, so their sign-in now acts as them.
/// </summary>
public sealed record StaffAccessLinked(
    StaffAccessId StaffAccessId,
    DateTimeOffset OccurredAt) : IDomainEvent;
