using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Organizations.Access.Events;

/// <summary>
/// A new invite replaced any open one and unlinked the previous sign-in.
/// </summary>
public sealed record StaffAccessReinvited(
    StaffAccessId StaffAccessId,
    DateTimeOffset OccurredAt) : IDomainEvent;
