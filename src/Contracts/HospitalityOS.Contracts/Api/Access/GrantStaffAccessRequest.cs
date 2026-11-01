using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Access;

/// <summary>
/// Gives someone access to the caller's tenant and invites them with a one-time code. Missing values are reported by the
/// server, not the deserializer.
/// </summary>
/// <param name="DisplayName">Who they are, e.g. Sam Patel.</param>
/// <param name="Role">What they may do.</param>
/// <param name="StaffMemberId">The roster entry they work as; required for workers.</param>
public sealed record GrantStaffAccessRequest(
    string? DisplayName = null,
    StaffRole? Role = null,
    StaffMemberId? StaffMemberId = null);
