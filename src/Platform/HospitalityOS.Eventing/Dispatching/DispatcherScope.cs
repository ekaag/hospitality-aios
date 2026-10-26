using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Eventing.Dispatching;

/// <summary>
/// The outbox for one dispatch pass, and the DI scope that owns it. Created by a factory registered in
/// <see cref="DependencyInjection"/>, so the dispatcher never touches the container (P15).
/// </summary>
internal sealed class DispatcherScope(IOutboxStore outbox, IAsyncDisposable owner) : IAsyncDisposable
{
    public IOutboxStore Outbox { get; } = outbox;

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
