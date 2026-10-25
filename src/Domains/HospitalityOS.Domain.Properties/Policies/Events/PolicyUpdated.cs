using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Properties.Policies.Events;

/// <summary>
/// A policy's content or status changed (spec §8, Journey 4). Carries the full new state, not a diff.
/// </summary>
public sealed record PolicyUpdated(
    PolicyId PolicyId,
    PolicyDetails Details,
    PolicyStatus Status,
    DateTimeOffset OccurredAt) : IDomainEvent;
