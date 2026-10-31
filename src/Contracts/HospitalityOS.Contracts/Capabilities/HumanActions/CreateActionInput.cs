using System.ComponentModel;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;

namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Input of <c>HumanAction.Create</c>. Who asked for the work is the caller, never the input.
/// </summary>
public sealed record CreateActionInput(
    [property: Description("The property where the work happens.")] PropertyId PropertyId,
    [property: Description("The kind of work.")] ActionType ActionType,
    [property: Description("What to do, as staff read it, e.g. Deliver 2 × Bath towel.")] string Instructions,
    [property: Description("Where, e.g. Room 412.")] ActionLocation Location,
    [property: Description("Why the work is needed.")] string Reason,
    [property: Description("How urgent; Normal unless there is a reason.")] ActionPriority Priority = ActionPriority.Normal,
    [property: Description("Minutes allowed from now to completion; sets the deadline when no deadline is given.")] int? SlaMinutes = null,
    [property: Description("When it must be done by (UTC); in the future.")] DateTimeOffset? Deadline = null,
    [property: Description("What to bring or restock; required for DeliverItems.")] ValueList<ActionItemEntry>? Items = null,
    [property: Description("Skill codes the router matches, e.g. housekeeping.")] ValueList<string>? SkillsRequired = null,
    [property: Description("The service request the work fulfils, if any.")] ServiceRequestId? ServiceRequestId = null,
    [property: Description("The event that triggered the request, if any.")] Guid? SourceEventId = null);
