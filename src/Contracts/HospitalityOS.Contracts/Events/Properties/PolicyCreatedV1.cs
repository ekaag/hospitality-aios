using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>PolicyCreated</c>: a policy was created (Journey 4).
/// </summary>
/// <param name="PolicyId">The policy.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record PolicyCreatedV1(
    Guid PolicyId,
    PropertyId PropertyId,
    PolicyDetailsData Details,
    string Status) : IEventPayload;
