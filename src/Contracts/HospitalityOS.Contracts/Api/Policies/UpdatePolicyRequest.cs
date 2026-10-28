using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities.Properties;

namespace HospitalityOS.Contracts.Api.Policies;

/// <summary>
/// <c>PUT /api/v1/policies/{id}</c>: replaces a draft or active policy's content; a retired policy cannot change.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="Type">What the policy governs; required.</param>
/// <param name="Title">Short title; required.</param>
/// <param name="Content">The authoritative text; required.</param>
/// <param name="EffectiveFrom">First property-local date it applies.</param>
/// <param name="EffectiveTo">Last date it applies (inclusive).</param>
/// <param name="AppliesTo">What it governs; omit for property-wide.</param>
public sealed record UpdatePolicyRequest(
    PolicyType? Type = null,
    string? Title = null,
    string? Content = null,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    SubjectReference? AppliesTo = null);
