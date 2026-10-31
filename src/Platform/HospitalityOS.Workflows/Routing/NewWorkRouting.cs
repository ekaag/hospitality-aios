using HospitalityOS.Contracts.Events;
using HospitalityOS.Contracts.Events.Operations;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Eventing.Handling;

namespace HospitalityOS.Workflows.Routing;

/// <summary>
/// Routes each newly requested human action to someone eligible (spec §9, Journey 9: no manager in the loop).
/// </summary>
internal sealed class NewWorkRouting(IHumanActionRepository actions, HumanActionRouter router) : IEventHandler<HumanActionRequestedV1>
{
    /// <summary>
    /// The handler's name, which the assignment records as who assigned the work.
    /// </summary>
    public const string Name = "humanaction.route";

    public async Task HandleAsync(EventEnvelope envelope, HumanActionRequestedV1 payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // A redelivery after someone assigned it by hand finds it no longer waiting.
        if (await actions.GetByIdAsync(new HumanActionId(payload.HumanActionId), cancellationToken).ConfigureAwait(false) is { Status: HumanActionStatus.Requested } action)
        {
            await router.RouteAsync(action, null, cancellationToken).ConfigureAwait(false);
        }
    }
}
