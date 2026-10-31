using System.ComponentModel;

namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Input of <c>HumanAction.UpdateStatus</c>: the assignee accepts, starts, or fails the work. The assignee is the caller.
/// </summary>
public sealed record UpdateActionStatusInput(
    [property: Description("The action.")] Guid HumanActionId,
    [property: Description("Accepted, InProgress, or Failed.")] ActionStatus Status,
    [property: Description("Why, when failing, e.g. Could not access the room.")] string? Reason = null);
