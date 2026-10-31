using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Ports.Execution;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.HumanActions;

/// <summary>
/// <c>HumanAction.UpdateStatus</c>: the assignee accepts, starts, or fails the work (Journeys 13 and 20), raising
/// <c>HumanActionAccepted</c>, <c>HumanActionStarted</c>, or <c>HumanActionFailed</c>. Only the assignee may, and a
/// status already reached is a successful no-op, so a repeated tap changes nothing (P8).
/// </summary>
internal sealed class UpdateActionStatusCapability(
    IHumanActionRepository actions,
    OperationContext operation,
    TimeProvider timeProvider) : ICapability<UpdateActionStatusInput, HumanActionOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "HumanAction.UpdateStatus",
        Version = 1,
        Description = "The assigned staff member accepts a human action, starts it, or fails it with a reason, such as being unable to access the room.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Worker, CapabilityCaller.Manager, CapabilityCaller.Admin),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<HumanActionOutput>> ExecuteAsync(UpdateActionStatusInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (HumanActionEntries.StaffMemberOf(operation.Actor) is not { } staff)
        {
            return HumanActionErrors.NotStaffMember;
        }

        if (await HumanActionEntries.FindAsync(actions, input.HumanActionId, cancellationToken).ConfigureAwait(false) is not { } action)
        {
            return HumanActionErrors.NotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result updated = input.Status switch
        {
            ActionStatus.Accepted => action.Accept(staff, now),
            ActionStatus.InProgress => action.Start(staff, now),
            ActionStatus.Failed => action.Fail(staff, input.Reason ?? "", now),
            _ => HumanActionErrors.StatusNotSettable,
        };

        return updated.IsFailure ? updated.Error : HumanActionEntries.Output(action);
    }
}
