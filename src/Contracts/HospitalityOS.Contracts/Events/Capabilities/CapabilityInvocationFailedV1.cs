namespace HospitalityOS.Contracts.Events.Capabilities;

/// <summary>
/// Version 1 of <c>CapabilityInvocationFailed</c>: the invocation ran and failed; nothing it attempted was saved.
/// </summary>
/// <param name="CapabilityInvocationId">The invocation.</param>
/// <param name="CapabilityName">The capability, e.g. <c>GuestService.CreateRequest</c>.</param>
/// <param name="CapabilityVersion">The capability version.</param>
/// <param name="ErrorCode">The error code, e.g. <c>humanaction.not_assigned</c>.</param>
public sealed record CapabilityInvocationFailedV1(
    Guid CapabilityInvocationId,
    string CapabilityName,
    int CapabilityVersion,
    string ErrorCode) : IEventPayload;
