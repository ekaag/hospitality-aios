namespace HospitalityOS.Contracts.Events.Organizations;

/// <summary>
/// Version 1 of <c>StaffAccessLinked</c>: someone redeemed their invite and now signs in as themselves.
/// </summary>
/// <param name="StaffAccessId">Their access.</param>
public sealed record StaffAccessLinkedV1(
    Guid StaffAccessId) : IEventPayload;
