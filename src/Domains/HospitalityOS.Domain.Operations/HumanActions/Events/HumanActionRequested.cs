using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;

namespace HospitalityOS.Domain.Operations.HumanActions.Events;

/// <summary>
/// Physical work was requested (spec §8); the HumanAction Router reacts by assigning it.
/// </summary>
public sealed record HumanActionRequested(
    HumanActionId HumanActionId,
    PropertyId PropertyId,
    ServiceRequestId? ServiceRequestId,
    HumanActionType ActionType,
    HumanActionPriority Priority,
    ActionLocation Location,
    DateTimeOffset? Deadline,
    ValueList<string> SkillsRequired,
    DateTimeOffset OccurredAt) : IDomainEvent;
