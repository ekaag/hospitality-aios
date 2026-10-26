using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.UnitTests.Eventing.TestDoubles;

/// <summary>
/// A transport, bound as the default, that records publishes and fails for chosen events.
/// </summary>
internal sealed class RecordingTransport : IEventTransport, IBindingResolver<IEventTransport>
{
    public List<EventMessage> Published { get; } = [];

    public HashSet<Guid> Failing { get; } = [];

    public IEventTransport Resolve(BindingContext context) => this;

    public Task PublishAsync(EventMessage message, CancellationToken cancellationToken)
    {
        if (Failing.Contains(message.EventId))
        {
            throw new InvalidOperationException("broker unavailable");
        }

        Published.Add(message);

        return Task.CompletedTask;
    }

    public Task<IAsyncDisposable> SubscribeAsync(string subscription, Func<EventMessage, CancellationToken, Task> handler, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<DeadLetteredEvent>> ReadDeadLettersAsync(string subscription, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
