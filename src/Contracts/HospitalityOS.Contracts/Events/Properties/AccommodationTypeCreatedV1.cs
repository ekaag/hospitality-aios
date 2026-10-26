using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>AccommodationTypeCreated</c>: an accommodation type (room type) was created.
/// </summary>
/// <param name="AccommodationTypeId">The accommodation type.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record AccommodationTypeCreatedV1(
    Guid AccommodationTypeId,
    PropertyId PropertyId,
    AccommodationTypeDetailsData Details) : IEventPayload;
