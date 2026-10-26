using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Capabilities.CapabilityInvocations.Events;

/// <summary>
/// An invocation ran and failed; nothing it attempted was saved.
/// </summary>
public sealed record CapabilityInvocationFailed(
    CapabilityInvocationId CapabilityInvocationId,
    string CapabilityName,
    int CapabilityVersion,
    string ErrorCode,
    DateTimeOffset OccurredAt) : IDomainEvent;
