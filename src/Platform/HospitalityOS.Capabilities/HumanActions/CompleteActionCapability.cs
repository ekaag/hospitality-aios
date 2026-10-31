using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Ports.Execution;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.HumanActions;

/// <summary>
/// <c>HumanAction.Complete</c>: the assignee records the work as done, with an optional note and evidence (Journey 14),
/// raising <c>HumanActionCompleted</c> exactly once: completing it again keeps the first completion and raises nothing
/// (P8), so the parent workflow resumes once.
/// </summary>
internal sealed class CompleteActionCapability(
    IHumanActionRepository actions,
    OperationContext operation,
    TimeProvider timeProvider) : ICapability<CompleteActionInput, HumanActionOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "HumanAction.Complete",
        Version = 1,
        Description = "The assigned staff member records a human action as done, with an optional note and photos.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Worker, CapabilityCaller.Manager, CapabilityCaller.Admin),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<HumanActionOutput>> ExecuteAsync(CompleteActionInput input, CancellationToken cancellationToken)
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

        Result completed = action.Complete(staff, input.Note, input.Evidence, timeProvider.GetUtcNow());

        return completed.IsFailure ? completed.Error : HumanActionEntries.Output(action);
    }
}
