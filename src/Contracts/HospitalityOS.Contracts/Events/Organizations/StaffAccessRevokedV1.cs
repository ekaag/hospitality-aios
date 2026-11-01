namespace HospitalityOS.Contracts.Events.Organizations;

/// <summary>
/// Version 1 of <c>StaffAccessRevoked</c>: someone's access ended.
/// </summary>
/// <param name="StaffAccessId">Their access.</param>
public sealed record StaffAccessRevokedV1(
    Guid StaffAccessId) : IEventPayload;
