using System.ComponentModel;

namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Input of <c>HumanAction.Claim</c>: the caller takes unassigned work they are eligible for.
/// </summary>
public sealed record ClaimActionInput(
    [property: Description("The action.")] Guid HumanActionId);
