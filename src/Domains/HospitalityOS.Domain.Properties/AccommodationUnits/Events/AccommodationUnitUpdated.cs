using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.AccommodationUnits.Events;

/// <summary>
/// An accommodation unit's number or type changed. Carries the full new state, not a diff.
/// </summary>
public sealed record AccommodationUnitUpdated(
    AccommodationUnitId AccommodationUnitId,
    AccommodationTypeId AccommodationTypeId,
    string UnitNumber,
    DateTimeOffset OccurredAt) : IDomainEvent;
