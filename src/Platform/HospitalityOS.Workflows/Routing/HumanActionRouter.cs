using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Workflows.Routing;

/// <summary>
/// The HumanAction Router's one move (spec §9): picks who should do a piece of work by <see cref="HumanActionRouting"/>
/// and assigns it to them through <c>HumanAction.Assign</c> (P7), as the agent of whichever routing step asked, which the
/// assignment history records. When nobody eligible is on duty the work stays where it is: unassigned work waits in the
/// unassigned queue until someone comes on duty.
/// </summary>
internal sealed partial class HumanActionRouter(
    IHumanActionRepository actions,
    IStaffMemberRepository staff,
    ICapabilityExecutor executor,
    ILogger<HumanActionRouter> logger)
{
    public const string AssignCapability = "HumanAction.Assign";

    /// <summary>
    /// Assigns <paramref name="action"/> to whoever the rule picks, never <paramref name="excluding"/>.
    /// </summary>
    /// <returns>Who it went to, or <see langword="null"/> when it stayed where it was.</returns>
    public async Task<StaffMemberId?> RouteAsync(HumanAction action, StaffMemberId? excluding, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        IReadOnlyList<StaffMember> roster = await staff.ListForPropertyAsync(action.PropertyId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<HumanAction> open = await actions.ListOpenForPropertyAsync(action.PropertyId, cancellationToken).ConfigureAwait(false);

        if (HumanActionRouting.Choose(action, roster, open, excluding) is not { } chosen)
        {
            LogNobodyEligible(action.Id.Value);

            return null;
        }

        Result<CapabilityResponse> assigned = await executor.ExecuteAsync(
            new CapabilityRequest(
                AssignCapability,
                JsonSerializer.SerializeToElement(new AssignActionInput(action.Id.Value, chosen.Id), ContractJson.Options),
                1),
            cancellationToken).ConfigureAwait(false);

        if (assigned.IsFailure)
        {
            // E.g. accepted meanwhile: the work has moved on, so there is nothing to route.
            LogRefused(action.Id.Value, assigned.Error.Code);

            return null;
        }

        return chosen.Id;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nobody eligible is on duty for human action {HumanActionId}; it stays where it is")]
    private partial void LogNobodyEligible(Guid humanActionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Human action {HumanActionId} was not assigned ({ErrorCode})")]
    private partial void LogRefused(Guid humanActionId, string errorCode);
}
