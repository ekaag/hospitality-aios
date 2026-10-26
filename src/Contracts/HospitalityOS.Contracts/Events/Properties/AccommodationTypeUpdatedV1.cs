using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>AccommodationTypeUpdated</c>: an accommodation type changed.
/// </summary>
/// <param name="AccommodationTypeId">The accommodation type.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record AccommodationTypeUpdatedV1(
    Guid AccommodationTypeId,
    AccommodationTypeDetailsData Details) : IEventPayload;
