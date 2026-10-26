using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;

namespace HospitalityOS.Contracts.Events.Operations;

/// <summary>
/// Version 1 of <c>HumanActionRequested</c>: physical work was requested (spec §7).
/// </summary>
/// <param name="HumanActionId">The human action.</param>
/// <param name="PropertyId">The property.</param>
/// <param name="ServiceRequestId">The service request.</param>
/// <param name="ActionType">The kind of work (domain enum name).</param>
/// <param name="Priority">The priority (domain enum name).</param>
/// <param name="Location">Where the work happens.</param>
/// <param name="Deadline">When the work must be done by (UTC), if set.</param>
/// <param name="SkillsRequired">Skills the assignee needs.</param>
public sealed record HumanActionRequestedV1(
    Guid HumanActionId,
    PropertyId PropertyId,
    ServiceRequestId? ServiceRequestId,
    string ActionType,
    string Priority,
    ActionLocation Location,
    DateTimeOffset? Deadline,
    ValueList<string> SkillsRequired) : IEventPayload;
