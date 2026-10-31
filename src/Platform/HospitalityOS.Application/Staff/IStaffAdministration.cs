using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Staff;

/// <summary>
/// Keeps the caller's tenant's rosters: who does physical work at each property, their skills, and whether they are on
/// duty, which is what the HumanAction Router routes by. Commands commit their change and its audit record together;
/// rejected commands are audited too. Scoped; the caller's tenant and actor come from the operation.
/// </summary>
public interface IStaffAdministration
{
    /// <summary>
    /// Adds someone to a property's roster and raises <c>StaffMemberCreated</c>.
    /// </summary>
    /// <returns>The staff member, or a <see cref="ValidationError"/> (a property not in the tenant is the field
    /// <c>propertyId</c>; an ID already on a roster is <c>staff_member.id_taken</c>).</returns>
    Task<Result<StaffMemberResponse>> CreateAsync(CreateStaffMemberRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a staff member's name and skills and raises <c>StaffMemberUpdated</c>; unchanged details are a no-op.
    /// </summary>
    /// <returns>The staff member; <c>staff_member.not_found</c>; <c>request.version_mismatch</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<StaffMemberResponse>> UpdateAsync(Guid id, UpdateStaffMemberRequest request, long? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a staff member on or off duty and raises <c>StaffMemberDutyChanged</c>; their current duty is a no-op. Workers
    /// may set only their own duty.
    /// </summary>
    /// <returns>The staff member; <c>staff_member.not_found</c>; <c>staff_member.duty_forbidden</c>; or a
    /// <see cref="ValidationError"/>.</returns>
    Task<Result<StaffMemberResponse>> SetDutyAsync(Guid id, SetDutyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a staff member.
    /// </summary>
    /// <returns>The staff member, or <c>staff_member.not_found</c>.</returns>
    Task<Result<StaffMemberResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// A property's roster, by name.
    /// </summary>
    Task<IReadOnlyList<StaffMemberResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
