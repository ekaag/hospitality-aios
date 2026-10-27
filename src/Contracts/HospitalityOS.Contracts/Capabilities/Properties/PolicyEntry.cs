namespace HospitalityOS.Contracts.Capabilities.Properties;

/// <summary>
/// An active policy in effect today.
/// </summary>
/// <param name="Source">The policy record.</param>
/// <param name="Type">Topic.</param>
/// <param name="Title">Title.</param>
/// <param name="Content">The authoritative text; quote or paraphrase it faithfully, never extend it.</param>
/// <param name="EffectiveFrom">First day in effect.</param>
/// <param name="EffectiveTo">Last day in effect.</param>
/// <param name="AppliesTo">What it applies to; property-wide when absent.</param>
public sealed record PolicyEntry(
    SourceReference Source,
    PolicyType Type,
    string Title,
    string Content,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    Subject? AppliesTo);
