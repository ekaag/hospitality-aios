using HospitalityOS.Contracts.Events;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;

namespace HospitalityOS.Eventing.Envelopes;

/// <summary>
/// The origin of every event committed in one scope. An event handler's scope continues the handled event's flow
/// (same correlation, caused by that event, acted on by the handler); any other scope starts a new correlation, acted
/// on by the system, until the ambient execution context (C7) supplies the caller's. Scoped.
/// </summary>
internal sealed class ScopedEventOrigin(IBindingResolver<IIdGenerator> idGenerators)
{
    private EventOrigin? current;

    public EventOrigin Current => current ??= new EventOrigin(idGenerators.Resolve().NewId(), CausationId: null, ActorType.System, ActorId: null);

    /// <summary>
    /// Makes every event committed in this scope part of <paramref name="origin"/>'s flow.
    /// </summary>
    public void Use(EventOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        current = origin;
    }
}
