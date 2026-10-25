using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;

namespace HospitalityOS.Domain.Operations.HumanActions;

/// <summary>
/// Everything needed to request a <see cref="HumanAction"/> (spec §7). Validated by <see cref="HumanAction.Request"/>.
/// </summary>
/// <param name="ActionType">The kind of work.</param>
/// <param name="Instructions">What to do, as staff read it, e.g. "Deliver 2 bath towels"; required.</param>
/// <param name="Location">Where, e.g. Room 412; required.</param>
/// <param name="Reason">Why the work is needed, e.g. "Guest requested towels via concierge"; required.</param>
/// <param name="RequestedByAgent">The controller or user that requested it, e.g. <c>GuestServiceController</c>; required.</param>
/// <param name="Priority">How urgent.</param>
/// <param name="Sla">Time allowed from request to completion; sets the deadline when <paramref name="Deadline"/> is not given.</param>
/// <param name="Deadline">Explicit deadline (UTC); must be in the future.</param>
/// <param name="Items">What to bring or restock; required for <see cref="HumanActionType.DeliverItems"/>.</param>
/// <param name="SkillsRequired">Skill codes the router matches, e.g. <c>housekeeping</c>, <c>hvac</c>.</param>
/// <param name="ServiceRequestId">The service request the work fulfils, if any; its controller resumes on completion.</param>
/// <param name="SourceEventId">The ID of the event that triggered the request, e.g. <c>ServiceRequestCreated</c>.</param>
public sealed record HumanActionRequest(
    HumanActionType ActionType,
    string Instructions,
    ActionLocation Location,
    string Reason,
    string RequestedByAgent,
    HumanActionPriority Priority = HumanActionPriority.Normal,
    TimeSpan? Sla = null,
    DateTimeOffset? Deadline = null,
    ValueList<ActionItem>? Items = null,
    ValueList<string>? SkillsRequired = null,
    ServiceRequestId? ServiceRequestId = null,
    Guid? SourceEventId = null);
