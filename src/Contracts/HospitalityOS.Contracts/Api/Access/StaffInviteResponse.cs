namespace HospitalityOS.Contracts.Api.Access;

/// <summary>
/// A new invite: the code is shown this once, for the administrator to pass on; only its hash is kept.
/// </summary>
/// <param name="Access">The access it opens.</param>
/// <param name="Code">The one-time code, e.g. K7QF3-MZP9A-...; redeemed after signing in.</param>
/// <param name="ExpiresAt">When it stops working.</param>
public sealed record StaffInviteResponse(
    StaffAccessResponse Access,
    string Code,
    DateTimeOffset ExpiresAt);
