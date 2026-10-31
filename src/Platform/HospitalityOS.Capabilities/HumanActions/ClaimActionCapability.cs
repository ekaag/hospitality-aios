using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Ports.Execution;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.HumanActions;

/// <summary>
/// <c>HumanAction.Claim</c>: a worker takes unassigned work from the staff surface's queue (J2), which assigns it to them
/// and accepts it in one step, raising <c>HumanActionAssigned</c> and <c>HumanActionAccepted</c>. Only the router's
/// eligible may (<see cref="HumanActionRouting.IsEligible"/>: on duty at the property with every required skill), only
/// unassigned work can be claimed, and claiming work one already holds is a successful no-op (P8).
/// </summary>
internal sealed class ClaimActionCapability(
    IHumanActionRepository actions,
    IStaffMemberRepository staff,
    OperationContext operation,
    TimeProvider timeProvider) : ICapability<ClaimActionInput, HumanActionOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Name = "HumanAction.Claim",
        Version = 1,
        Description = "A staff member takes unassigned work they are eligible for (on duty at the property with every required skill), "
            + "which assigns it to them and accepts it.",
        Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Worker),
        ConfirmationPolicy = ConfirmationPolicy.NotRequired,
        SideEffects = SideEffect.PlatformState,
        Idempotency = IdempotencyPolicy.Inherent,
        AuditClassification = AuditClassification.Routine,
    };

    public async Task<Result<HumanActionOutput>> ExecuteAsync(ClaimActionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (HumanActionEntries.StaffMemberOf(operation.Actor) is not { } staffMemberId)
        {
            return HumanActionErrors.NotStaffMember;
        }

        if (await HumanActionEntries.FindAsync(actions, input.HumanActionId, cancellationToken).ConfigureAwait(false) is not { } action)
        {
            return HumanActionErrors.NotFound;
        }

        if (action.AssignedTo == staffMemberId && action.Status is HumanActionStatus.Accepted or HumanActionStatus.InProgress)
        {
            return HumanActionEntries.Output(action);
        }

        if (action.Status != HumanActionStatus.Requested)
        {
            return HumanActionErrors.NotUnassigned;
        }

        if (await staff.GetByIdAsync(staffMemberId, cancellationToken).ConfigureAwait(false) is not { } member
            || !HumanActionRouting.IsEligible(member, action))
        {
            return HumanActionErrors.NotEligible;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result claimed = action.Assign(staffMemberId, HumanActionEntries.Name(operation.Actor), now);

        if (claimed.IsSuccess)
        {
            claimed = action.Accept(staffMemberId, now);
        }

        return claimed.IsFailure ? claimed.Error : HumanActionEntries.Output(action);
    }
}
