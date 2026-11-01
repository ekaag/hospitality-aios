namespace HospitalityOS.Contracts.Api.Access;

/// <summary>
/// Links the caller's sign-in to the access an invite opens.
/// </summary>
/// <param name="Code">The invite code, with or without its hyphens.</param>
public sealed record RedeemInviteRequest(string? Code = null);
