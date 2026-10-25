using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.AccommodationUnits.Events;

/// <summary>
/// An accommodation unit was created.
/// </summary>
public sealed record AccommodationUnitCreated(
    AccommodationUnitId AccommodationUnitId,
    PropertyId PropertyId,
    AccommodationTypeId AccommodationTypeId,
    SpaceId SpaceId,
    string UnitNumber,
    DateTimeOffset OccurredAt) : IDomainEvent;
