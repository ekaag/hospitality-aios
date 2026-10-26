using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>PropertyUpdated</c>: a property's base information changed.
/// </summary>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record PropertyUpdatedV1(
    PropertyId PropertyId,
    PropertyDetailsData Details) : IEventPayload;
