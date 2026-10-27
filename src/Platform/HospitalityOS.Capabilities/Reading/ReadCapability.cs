using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;

namespace HospitalityOS.Capabilities.Reading;

/// <summary>
/// What every read capability declares: anyone in the tenant may read property information (guests through the
/// concierge above all), reads change nothing, need no confirmation or key, and are audited routinely.
/// </summary>
internal static class ReadCapability
{
    private static readonly CapabilityAuthorization everyone = CapabilityAuthorization.Allow(
        CapabilityCaller.System,
        CapabilityCaller.Agent,
        CapabilityCaller.Guest,
        CapabilityCaller.Worker,
        CapabilityCaller.Manager,
        CapabilityCaller.Admin);

    public static CapabilityDescriptor Describe(string name, string description) => new()
    {
        Name = name,
        Version = 1,
        Description = description,
        Authorization = everyone,
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.None,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };
}
