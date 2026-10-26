using HospitalityOS.Contracts.Events.Values;

namespace HospitalityOS.Contracts.Events.Properties;

/// <summary>
/// Version 1 of <c>PolicyUpdated</c>: a policy changed, was activated, or was retired.
/// </summary>
/// <param name="PolicyId">The policy.</param>
/// <param name="Details">The full, normalized details after the change.</param>
/// <param name="Status">The status after the change (domain enum name).</param>
public sealed record PolicyUpdatedV1(
    Guid PolicyId,
    PolicyDetailsData Details,
    string Status) : IEventPayload;
