using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Domain.Properties.AccommodationTypes.Events;

/// <summary>
/// An accommodation type was created.
/// </summary>
public sealed record AccommodationTypeCreated(
    AccommodationTypeId AccommodationTypeId,
    PropertyId PropertyId,
    AccommodationTypeDetails Details,
    DateTimeOffset OccurredAt) : IDomainEvent;
