using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Policies.Events;

/// <summary>
/// A policy was created, as a draft (spec §8).
/// </summary>
public sealed record PolicyCreated(
    PolicyId PolicyId,
    PropertyId PropertyId,
    PolicyDetails Details,
    PolicyStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
