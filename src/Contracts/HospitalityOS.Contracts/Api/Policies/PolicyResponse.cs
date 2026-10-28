using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Policies;

/// <summary>
/// A policy, normalized (trimmed).
/// </summary>
/// <param name="Id">Platform-owned ID.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="Type">What the policy governs.</param>
/// <param name="Title">Short title.</param>
/// <param name="Content">The authoritative text.</param>
/// <param name="EffectiveFrom">First property-local date it applies.</param>
/// <param name="EffectiveTo">Last date it applies (inclusive).</param>
/// <param name="AppliesTo">What it governs; <c>null</c> for property-wide.</param>
/// <param name="Status">Where it is in its lifecycle.</param>
/// <param name="Version">Increases with every change; send it back in <c>If-Match</c> to update safely.</param>
public sealed record PolicyResponse(
    Guid Id,
    PropertyId PropertyId,
    PolicyType Type,
    string Title,
    string Content,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    SubjectReference? AppliesTo,
    PolicyStatus Status,
    long Version);
