using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.Eventing.Scheduling;
using HospitalityOS.SharedKernel.Ids;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Workflows.Routing;

/// <summary>
/// Gives every assignment, by the router or by a manager, a deadline to be accepted: when it passes with the work still
/// unaccepted by the same assignee, <see cref="UnacceptedRerouting"/> hands it to someone else eligible.
/// </summary>
internal sealed class AcceptanceWatch(IWorkScheduler scheduler, IOptions<HumanActionRoutingOptions> options) : IEventHandler<HumanActionAssignedV1>
{
    public const string Name = "humanaction.watch_acceptance";

    public Task HandleAsync(EventEnvelope envelope, HumanActionAssignedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(payload);

        return scheduler.ScheduleAsync(
            UnacceptedRerouting.Name,
            new AcceptanceDue(payload.HumanActionId, payload.StaffMemberId, envelope.OccurredAt),
            envelope.OccurredAt + options.Value.AcceptanceTimeout,
            cancellationToken);
    }
}

/// <summary>
/// An assignment whose acceptance deadline has come.
/// </summary>
/// <param name="HumanActionId">The work.</param>
/// <param name="StaffMemberId">Who it was assigned to.</param>
/// <param name="AssignedAt">When, which tells this assignment from a later one to the same person.</param>
internal sealed record AcceptanceDue(Guid HumanActionId, StaffMemberId StaffMemberId, DateTimeOffset AssignedAt);

/// <summary>
/// Hands work on when its assignee has not accepted it in time. Nothing happens when the work has moved on (accepted,
/// reassigned, or ended) or nobody else eligible is on duty; then it stays with them for a manager to see.
/// </summary>
internal sealed class UnacceptedRerouting(IHumanActionRepository actions, HumanActionRouter router) : IScheduledHandler<AcceptanceDue>
{
    /// <summary>
    /// The handler's name, which the assignment records as who reassigned the work.
    /// </summary>
    public const string Name = "humanaction.reroute_unaccepted";

    public async Task HandleAsync(AcceptanceDue payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (await actions.GetByIdAsync(new HumanActionId(payload.HumanActionId), cancellationToken).ConfigureAwait(false) is { Status: HumanActionStatus.Assigned } action
            && action.Assignments[^1] is { } current
            && current.StaffMemberId == payload.StaffMemberId
            && current.AssignedAt == payload.AssignedAt)
        {
            await router.RouteAsync(action, payload.StaffMemberId, cancellationToken).ConfigureAwait(false);
        }
    }
}
