using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Properties.Events;

/// <summary>
/// A property's base information changed (spec §8). Carries the full new details, not a diff.
/// </summary>
public sealed record PropertyUpdated(PropertyId PropertyId, PropertyDetails Details, DateTimeOffset OccurredAt) : IDomainEvent;
