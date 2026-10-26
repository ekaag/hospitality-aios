namespace HospitalityOS.Contracts.Events.Capabilities;

/// <summary>
/// Version 1 of <c>CapabilityInvocationRequested</c>: a capability invocation was accepted and is running. Carries no input,
/// which may hold guest data (P10).
/// </summary>
/// <param name="CapabilityInvocationId">The invocation.</param>
/// <param name="CapabilityName">The capability, e.g. <c>GuestService.CreateRequest</c>.</param>
/// <param name="CapabilityVersion">The capability version.</param>
public sealed record CapabilityInvocationRequestedV1(
    Guid CapabilityInvocationId,
    string CapabilityName,
    int CapabilityVersion) : IEventPayload;
