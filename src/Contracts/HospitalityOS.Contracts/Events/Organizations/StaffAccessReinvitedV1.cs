namespace HospitalityOS.Contracts.Events.Organizations;

/// <summary>
/// Version 1 of <c>StaffAccessReinvited</c>: a new invite replaced any open one and unlinked the previous sign-in.
/// </summary>
/// <param name="StaffAccessId">Their access.</param>
public sealed record StaffAccessReinvitedV1(
    Guid StaffAccessId) : IEventPayload;
