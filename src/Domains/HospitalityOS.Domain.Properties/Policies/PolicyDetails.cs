using HospitalityOS.Domain.Properties.Shared;

namespace HospitalityOS.Domain.Properties.Policies;

/// <summary>
/// A policy's content (Journey 4). Validated and normalized by <see cref="Policy"/>.
/// </summary>
/// <param name="Type">What the policy governs.</param>
/// <param name="Title">Short title, e.g. <c>Gym age policy</c>; required.</param>
/// <param name="Content">The authoritative policy text the concierge answers from; required.</param>
/// <param name="EffectiveFrom">First property-local date the policy applies; <c>null</c> for immediately.</param>
/// <param name="EffectiveTo">Last property-local date it applies (inclusive); <c>null</c> for no end.</param>
/// <param name="AppliesTo">The amenity, space, restaurant, or service it governs; <c>null</c> for property-wide.</param>
public sealed record PolicyDetails(
    PolicyType Type,
    string Title,
    string Content,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    SubjectReference? AppliesTo = null);
