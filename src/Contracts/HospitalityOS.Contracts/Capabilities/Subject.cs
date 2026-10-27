namespace HospitalityOS.Contracts.Capabilities;

/// <summary>
/// What a record is about.
/// </summary>
/// <param name="Kind">The kind of thing.</param>
/// <param name="Id">Its ID.</param>
public sealed record Subject(
    SubjectKind Kind,
    Guid Id);
