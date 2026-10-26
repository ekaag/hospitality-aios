using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Capabilities.CapabilityInvocations.Events;

/// <summary>
/// An invocation ran and its effects were saved.
/// </summary>
public sealed record CapabilityInvocationSucceeded(
    CapabilityInvocationId CapabilityInvocationId,
    string CapabilityName,
    int CapabilityVersion,
    DateTimeOffset OccurredAt) : IDomainEvent;
