namespace HospitalityOS.Contracts.Api.Access;

/// <summary>
/// Where someone's access stands.
/// </summary>
public enum StaffAccessStatus
{
    /// <summary>
    /// Invited; they have not redeemed the code yet.
    /// </summary>
    Invited,

    /// <summary>
    /// Their sign-in is linked and acts as them.
    /// </summary>
    Active,

    /// <summary>
    /// Revoked; it opens nothing.
    /// </summary>
    Revoked,
}
