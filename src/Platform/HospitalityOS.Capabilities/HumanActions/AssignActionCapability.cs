using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Ports.Execution;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.HumanActions;

/// <summary>
/// <c>HumanAction.Assign</c>: assigns or reassigns work not yet accepted, raising <c>HumanActionAssigned</c>; the caller is
/// recorded as who decided. Assigning the current assignee again changes nothing (P8).
/// </summary>
internal sealed class AssignActionCapability(
    IHumanActionRepository actions,
    OperationContext operation,
    TimeProvider timeProvider) : ICapability<AssignActionInput, HumanActionOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "HumanAction.Assign",
        Version = 1,
        Description = "Assigns a human action to a staff member, or reassigns it before it is accepted.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Manager, CapabilityCaller.Admin),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<HumanActionOutput>> ExecuteAsync(AssignActionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (await HumanActionEntries.FindAsync(actions, input.HumanActionId, cancellationToken).ConfigureAwait(false) is not { } action)
        {
            return HumanActionErrors.NotFound;
        }

        Result assigned = action.Assign(input.StaffMemberId, HumanActionEntries.Name(operation.Actor), timeProvider.GetUtcNow());

        return assigned.IsFailure ? assigned.Error : HumanActionEntries.Output(action);
    }
}
