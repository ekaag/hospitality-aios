using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Api.Work;

/// <summary>
/// A piece of work as staff read it (spec §32's action card).
/// </summary>
/// <param name="Id">The human action.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="ActionType">The kind of work.</param>
/// <param name="Status">Where it is.</param>
/// <param name="Instructions">What to do, e.g. <c>Deliver 2 × Bath towel</c>.</param>
/// <param name="Location">Where, e.g. <c>Room 412</c>.</param>
/// <param name="Reason">Why it is needed.</param>
/// <param name="Priority">How urgent.</param>
/// <param name="Items">What to bring.</param>
/// <param name="SkillsRequired">The skills it needs.</param>
/// <param name="RequestedAt">When it was asked for (UTC).</param>
/// <param name="Deadline">When it must be done by (UTC), if it has a deadline.</param>
/// <param name="AssignedTo">Who holds it, if anyone.</param>
/// <param name="AssigneeName">Their name, if they are on the roster.</param>
/// <param name="EndedAt">When it was completed, failed, cancelled, or expired (UTC).</param>
/// <param name="CompletionNote">The worker's note on completing it.</param>
/// <param name="FailureReason">Why it failed or was cancelled.</param>
/// <param name="Actions">What the caller may do to it now.</param>
public sealed record WorkItemResponse(
    Guid Id,
    PropertyId PropertyId,
    ActionType ActionType,
    ActionStatus Status,
    string Instructions,
    string Location,
    string Reason,
    ActionPriority Priority,
    ValueList<ActionItemEntry> Items,
    ValueList<string> SkillsRequired,
    DateTimeOffset RequestedAt,
    DateTimeOffset? Deadline,
    StaffMemberId? AssignedTo,
    string? AssigneeName,
    DateTimeOffset? EndedAt,
    string? CompletionNote,
    string? FailureReason,
    ValueList<WorkAction> Actions);
