using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.Domain.Capabilities.CapabilityInvocations.Events;

/// <summary>
/// A caller invoked a capability and it was accepted: known, authorized, confirmed if required, with valid input.
/// </summary>
public sealed record CapabilityInvocationRequested(
    CapabilityInvocationId CapabilityInvocationId,
    string CapabilityName,
    int CapabilityVersion,
    DateTimeOffset OccurredAt) : IDomainEvent;
