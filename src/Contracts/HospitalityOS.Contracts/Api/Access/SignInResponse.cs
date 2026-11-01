using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Access;

/// <summary>
/// Who the caller's sign-in acts as: nobody yet until they redeem an invite.
/// </summary>
/// <param name="IsLinked">Whether the sign-in acts as someone with access.</param>
/// <param name="IsOperator">Whether they operate the platform (they may invite a customer's first administrator).</param>
/// <param name="TenantId">Their tenant, once linked.</param>
/// <param name="AccessId">Their access, once linked.</param>
/// <param name="DisplayName">Who they are, once linked.</param>
/// <param name="Role">What they may do, once linked.</param>
/// <param name="StaffMemberId">The roster entry they work as, if any.</param>
public sealed record SignInResponse(
    bool IsLinked,
    bool IsOperator,
    Guid? TenantId,
    Guid? AccessId,
    string? DisplayName,
    StaffRole? Role,
    StaffMemberId? StaffMemberId);
