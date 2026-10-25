using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.Spaces.Events;

/// <summary>
/// A space's descriptive information changed. Carries the full new details, not a diff.
/// </summary>
public sealed record SpaceUpdated(SpaceId SpaceId, SpaceDetails Details, DateTimeOffset OccurredAt) : IDomainEvent;
