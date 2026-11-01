using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Access;

/// <summary>
/// Someone's access to the tenant. It never shows the invite code or how they sign in.
/// </summary>
/// <param name="Id">The access ID.</param>
/// <param name="DisplayName">Who they are.</param>
/// <param name="Role">What they may do.</param>
/// <param name="StaffMemberId">The roster entry they work as, if any.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="InviteExpiresAt">When the open invite stops working, while one is open.</param>
/// <param name="LinkedAt">When their sign-in was linked.</param>
/// <param name="RevokedAt">When it was revoked.</param>
/// <param name="Version">The version.</param>
public sealed record StaffAccessResponse(
    Guid Id,
    string DisplayName,
    StaffRole Role,
    StaffMemberId? StaffMemberId,
    StaffAccessStatus Status,
    DateTimeOffset? InviteExpiresAt,
    DateTimeOffset? LinkedAt,
    DateTimeOffset? RevokedAt,
    long Version);
