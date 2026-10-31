using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Capabilities.HumanActions;

/// <summary>
/// Human actions as the capabilities return them, and the lookups they share.
/// </summary>
internal static class HumanActionEntries
{
    public static HumanActionOutput Output(HumanAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        HumanActionRequest details = action.Details;

        return new HumanActionOutput(new HumanActionEntry(
            Sources.Of(action),
            EnumMirror.To<ActionType>(details.ActionType),
            EnumMirror.To<ActionStatus>(action.Status),
            details.Instructions,
            details.Location,
            details.Reason,
            EnumMirror.To<ActionPriority>(details.Priority),
            action.Deadline,
            ValueList.From((details.Items ?? []).Select(item => new ActionItemEntry(item.Name, item.Quantity, item.Code))),
            details.SkillsRequired ?? [],
            action.AssignedTo,
            details.RequestedByAgent,
            details.ServiceRequestId,
            action.FailureReason,
            details.CorrelationId));
    }

    /// <summary>
    /// The action in the current tenant, or <see langword="null"/>; an empty ID names none.
    /// </summary>
    public static async Task<HumanAction?> FindAsync(IHumanActionRepository actions, Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? null : await actions.GetByIdAsync(new HumanActionId(id), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// The staff member an actor is: staff whose ID is a staff member ID. Everyone else does the work as nobody.
    /// </summary>
    public static StaffMemberId? StaffMemberOf(Actor actor) =>
        actor is { Type: ActorType.Staff, Id: { } id } && Guid.TryParse(id, out Guid value) && value != Guid.Empty
            ? new StaffMemberId(value)
            : null;

    /// <summary>
    /// Who the caller is, as the action's history records it: the agent's name or the staff member's ID.
    /// </summary>
    public static string Name(Actor actor) => actor.Id ?? actor.Type.ToString();
}
