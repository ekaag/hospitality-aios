using System.ComponentModel;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Input of <c>HumanAction.Complete</c>. The assignee completing it is the caller.
/// </summary>
public sealed record CompleteActionInput(
    [property: Description("The action.")] Guid HumanActionId,
    [property: Description("The worker's note, e.g. Left at the door.")] string? Note = null,
    [property: Description("Keys of uploaded photos or files.")] ValueList<string>? Evidence = null);
