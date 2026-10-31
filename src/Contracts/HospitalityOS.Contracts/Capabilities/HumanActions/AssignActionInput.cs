using System.ComponentModel;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Contracts.Capabilities.HumanActions;

/// <summary>
/// Input of <c>HumanAction.Assign</c>. Who decided is the caller, never the input.
/// </summary>
public sealed record AssignActionInput(
    [property: Description("The action.")] Guid HumanActionId,
    [property: Description("Who should do the work.")] StaffMemberId StaffMemberId);
