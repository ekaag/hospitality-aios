using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Events.Organizations;

/// <summary>
/// Version 1 of <c>StaffAccessGranted</c>: someone was given access to the tenant and invited.
/// </summary>
/// <param name="StaffAccessId">Their access.</param>
/// <param name="Role">Their role: Worker, Manager, or Admin.</param>
/// <param name="StaffMemberId">The roster entry a worker works as.</param>
public sealed record StaffAccessGrantedV1(
    Guid StaffAccessId,
    string Role,
    StaffMemberId? StaffMemberId) : IEventPayload;
