namespace HospitalityOS.Domain.Properties.Policies;

/// <summary>
/// Lifecycle of a <see cref="Policy"/>: Draft → Active → Retired. Only active policies answer guests.
/// </summary>
public enum PolicyStatus
{
    /// <summary>
    /// Being written; not used for answers.
    /// </summary>
    Draft,

    /// <summary>
    /// In force (within its effective dates).
    /// </summary>
    Active,

    /// <summary>
    /// No longer in force; kept for traces and audit, and cannot be edited or reactivated.
    /// </summary>
    Retired,
}
