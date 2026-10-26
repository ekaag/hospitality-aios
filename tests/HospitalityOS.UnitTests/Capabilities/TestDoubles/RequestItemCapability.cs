using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;

namespace HospitalityOS.UnitTests.Capabilities.TestDoubles;

/// <summary>
/// A valid state-changing capability; <see cref="Descriptor"/> is the base the definition tests vary.
/// </summary>
internal sealed class RequestItemCapability : ICapability<RequestItemInput, RequestItemOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "GuestService.RequestItem",
        Version = 1,
        Description = "Asks housekeeping to bring the guest an item.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Guest),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.KeyRequired,
        AuditClassification = AuditClassification.Routine,
    };
}
