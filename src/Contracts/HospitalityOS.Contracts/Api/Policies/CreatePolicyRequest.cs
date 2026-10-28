using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Policies;

/// <summary>
/// <c>POST /api/v1/policies</c>: drafts an authoritative rule of the property (Journey 4), kept apart from descriptive
/// content. It answers guests once activated.
/// Every field may be omitted: missing required fields are reported by validation, field by field.
/// </summary>
/// <param name="PropertyId">The property; required.</param>
/// <param name="Type">What the policy governs; required.</param>
/// <param name="Title">Short title, e.g. <c>Gym age policy</c>; required.</param>
/// <param name="Content">The authoritative text the concierge answers from; required.</param>
/// <param name="EffectiveFrom">First property-local date it applies; omit for immediately.</param>
/// <param name="EffectiveTo">Last date it applies (inclusive); omit for no end.</param>
/// <param name="AppliesTo">The space, amenity, restaurant, or service it governs; omit for property-wide.</param>
public sealed record CreatePolicyRequest(
    PropertyId? PropertyId = null,
    PolicyType? Type = null,
    string? Title = null,
    string? Content = null,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    SubjectReference? AppliesTo = null);
