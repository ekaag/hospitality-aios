using HospitalityOS.Contracts.Events;

namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// Calls one registered handler with the envelope's typed payload. Registered scoped, keyed by handler name.
/// </summary>
internal interface IEventHandlerInvoker
{
    Task InvokeAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}
