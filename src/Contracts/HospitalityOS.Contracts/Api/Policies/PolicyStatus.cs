namespace HospitalityOS.Contracts.Api.Policies;

/// <summary>
/// Where a policy is in its lifecycle: Draft, then Active, then Retired. Only active policies answer guests.
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
    /// No longer in force; cannot be edited or reactivated.
    /// </summary>
    Retired,
}
