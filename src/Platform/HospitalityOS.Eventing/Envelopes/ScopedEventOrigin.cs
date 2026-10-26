using HospitalityOS.Contracts.Events;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;

namespace HospitalityOS.Eventing.Envelopes;

/// <summary>
/// The origin of every event committed in one scope: a new correlation per request or message, caused by nothing,
/// acted on by the system. Placeholder until the ambient execution context (C7) supplies the caller's correlation,
/// causation, and actor. Scoped.
/// </summary>
internal sealed class ScopedEventOrigin(IBindingResolver<IIdGenerator> idGenerators)
{
    private EventOrigin? current;

    public EventOrigin Current => current ??= new EventOrigin(idGenerators.Resolve().NewId(), CausationId: null, ActorType.System, ActorId: null);
}
