namespace HospitalityOS.Contracts.Events.Capabilities;

/// <summary>
/// Version 1 of <c>CapabilityInvocationSucceeded</c>: the invocation ran and its effects were saved. Carries no output, which
/// may hold guest data (P10).
/// </summary>
/// <param name="CapabilityInvocationId">The invocation.</param>
/// <param name="CapabilityName">The capability, e.g. <c>GuestService.CreateRequest</c>.</param>
/// <param name="CapabilityVersion">The capability version.</param>
public sealed record CapabilityInvocationSucceededV1(
    Guid CapabilityInvocationId,
    string CapabilityName,
    int CapabilityVersion) : IEventPayload;
