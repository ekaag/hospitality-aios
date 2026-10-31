using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// Gives work to someone.
/// </summary>
/// <param name="StaffMemberId">Who should do it; required.</param>
public sealed record AssignWorkRequest(StaffMemberId? StaffMemberId = null);
