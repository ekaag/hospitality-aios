using HospitalityOS.Contracts.Events;

namespace HospitalityOS.Eventing.Handling;

internal sealed class EventHandlerInvoker<TPayload, THandler>(THandler handler) : IEventHandlerInvoker
    where TPayload : IEventPayload
    where THandler : IEventHandler<TPayload>
{
    public Task InvokeAsync(EventEnvelope envelope, CancellationToken cancellationToken) =>
        handler.HandleAsync(envelope, (TPayload)envelope.Payload, cancellationToken);
}
