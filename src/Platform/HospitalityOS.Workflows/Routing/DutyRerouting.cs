using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Eventing.Handling;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Workflows.Routing;

/// <summary>
/// Reroutes work when the people on duty change: whoever comes on duty, or joins the roster on duty, may pick up the
/// property's unassigned work, oldest first; whoever goes off duty hands on work they have not accepted yet. Work they
/// accepted stays theirs to finish or fail.
/// </summary>
internal sealed class DutyRerouting(IHumanActionRepository actions, HumanActionRouter router)
    : IEventHandler<StaffMemberDutyChangedV1>, IEventHandler<StaffMemberCreatedV1>
{
    /// <summary>
    /// The handler's name for duty changes, which the assignment records as who assigned the work.
    /// </summary>
    public const string Name = "humanaction.reroute_on_duty";

    /// <summary>
    /// The handler's name for people joining the roster.
    /// </summary>
    public const string JoinedName = "humanaction.route_to_new_staff";

    public Task HandleAsync(EventEnvelope envelope, StaffMemberDutyChangedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload.IsOnDuty
            ? RouteWaitingAsync(payload.PropertyId, cancellationToken)
            : HandOnAsync(payload.PropertyId, payload.StaffMemberId, cancellationToken);
    }

    public Task HandleAsync(EventEnvelope envelope, StaffMemberCreatedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return payload.IsOnDuty ? RouteWaitingAsync(payload.PropertyId, cancellationToken) : Task.CompletedTask;
    }

    // Each routing reloads the open work, so the load of whoever took the last piece counts for the next.
    private async Task RouteWaitingAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<HumanAction> open = await actions.ListOpenForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        foreach (HumanAction action in open.Where(action => action.Status == HumanActionStatus.Requested))
        {
            await router.RouteAsync(action, null, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandOnAsync(PropertyId propertyId, StaffMemberId leaving, CancellationToken cancellationToken)
    {
        IReadOnlyList<HumanAction> open = await actions.ListOpenForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        foreach (HumanAction action in open.Where(action => action.Status == HumanActionStatus.Assigned && action.AssignedTo == leaving))
        {
            await router.RouteAsync(action, leaving, cancellationToken).ConfigureAwait(false);
        }
    }
}
