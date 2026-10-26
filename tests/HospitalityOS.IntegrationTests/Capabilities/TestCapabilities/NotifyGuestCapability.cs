using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationTests.Capabilities.TestCapabilities;

internal sealed record NotifyGuestInput(Guid StayId, string Text);

internal sealed record NotifyGuestOutput(Guid MessageId);

/// <summary>
/// An external, confirmed, sensitive capability that changes no aggregate: its invocation record is its only write, so
/// two racing runs with one key collide on the invocation's unique key.
/// </summary>
internal sealed class NotifyGuestCapability(CapabilityProbe probe) : ICapability<NotifyGuestInput, NotifyGuestOutput>
{
    public const string Name = "Messaging.NotifyGuest";

    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = Name,
        Version = 1,
        Description = "Sends the guest a message.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.System),
        ConfirmationPolicy = ConfirmationPolicy.Required,
        SideEffects = SideEffect.External,
        Idempotency = IdempotencyPolicy.KeyRequired,
        AuditClassification = AuditClassification.Sensitive,
    };

    public async Task<Result<NotifyGuestOutput>> ExecuteAsync(NotifyGuestInput input, CancellationToken cancellationToken)
    {
        probe.Ran(Name);
        await probe.ArriveAsync(cancellationToken);

        return new NotifyGuestOutput(Guid.CreateVersion7());
    }
}
