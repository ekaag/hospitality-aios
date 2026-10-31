using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;

namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// A human action as it stands: what staff must do, where, by when, and who holds it.
/// </summary>
/// <param name="Source">The action record; its ID is the action ID.</param>
/// <param name="ActionType">The kind of work.</param>
/// <param name="Status">Where it is.</param>
/// <param name="Instructions">What to do, as staff read it.</param>
/// <param name="Location">Where.</param>
/// <param name="Reason">Why the work is needed.</param>
/// <param name="Priority">How urgent.</param>
/// <param name="Deadline">When it must be done by (UTC), if it has a deadline.</param>
/// <param name="Items">What to bring or restock.</param>
/// <param name="SkillsRequired">Skill codes the router matches.</param>
/// <param name="AssignedTo">The current assignee, if any.</param>
/// <param name="RequestedBy">The agent or user that requested it.</param>
/// <param name="ServiceRequestId">The service request it fulfils, if any.</param>
/// <param name="FailureReason">Why it failed or was cancelled, if it did.</param>
/// <param name="CorrelationId">The flow the work belongs to: pass it as the flow to continue when acting on the work.</param>
public sealed record HumanActionEntry(
    SourceReference Source,
    ActionType ActionType,
    ActionStatus Status,
    string Instructions,
    ActionLocation Location,
    string Reason,
    ActionPriority Priority,
    DateTimeOffset? Deadline,
    ValueList<ActionItemEntry> Items,
    ValueList<string> SkillsRequired,
    StaffMemberId? AssignedTo,
    string RequestedBy,
    ServiceRequestId? ServiceRequestId,
    string? FailureReason,
    Guid? CorrelationId);
