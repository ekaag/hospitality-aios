namespace HospitalityOS.Contracts.Events.Values;

/// <summary>
/// A policy's authoritative content.
/// </summary>
/// <param name="Type">Children, pets, ... (domain enum name).</param>
/// <param name="Title">Title.</param>
/// <param name="Content">The authoritative policy text.</param>
/// <param name="EffectiveFrom">First effective date, if limited.</param>
/// <param name="EffectiveTo">Last effective date, if limited.</param>
/// <param name="AppliesTo">What it applies to, if anything specific.</param>
public sealed record PolicyDetailsData(
    string Type,
    string Title,
    string Content,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    SubjectReferenceData? AppliesTo);
