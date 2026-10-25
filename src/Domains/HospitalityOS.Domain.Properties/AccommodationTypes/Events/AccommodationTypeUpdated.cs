using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Properties.AccommodationTypes.Events;

/// <summary>
/// An accommodation type's descriptive information changed. Carries the full new details, not a diff.
/// </summary>
public sealed record AccommodationTypeUpdated(
    AccommodationTypeId AccommodationTypeId,
    AccommodationTypeDetails Details,
    DateTimeOffset OccurredAt) : IDomainEvent;
