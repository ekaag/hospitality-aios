using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Eventing.Replay;

/// <summary>
/// The event store for one replay, and the DI scope that owns it (created by a factory in <see cref="DependencyInjection"/>).
/// </summary>
internal sealed class ReplayScope(IEventStore eventStore, IAsyncDisposable owner) : IAsyncDisposable
{
    public IEventStore EventStore { get; } = eventStore;

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
