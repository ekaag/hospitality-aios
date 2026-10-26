using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>PropertyCreated</c>: a property (hotel) was created (Journey 1).
/// </summary>
/// <param name="PropertyId">The property.</param>
/// <param name="OrganizationId">The organization.</param>
/// <param name="Details">The full, normalized details after the change.</param>
public sealed record PropertyCreatedV1(
    PropertyId PropertyId,
    OrganizationId OrganizationId,
    PropertyDetailsData Details) : IEventPayload;
