using HospitalityOS.Eventing.Envelopes;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// Everything one handler invocation needs from its own DI scope, resolved by a factory registered in
/// <see cref="DependencyInjection"/> so no class here holds the container (P15).
/// </summary>
internal sealed class HandlerScope(
    IEventHandlerInvoker handler,
    IInboxStore inbox,
    IUnitOfWork unitOfWork,
    TenantContext tenant,
    ScopedEventOrigin origin,
    IAsyncDisposable owner) : IAsyncDisposable
{
    public IEventHandlerInvoker Handler { get; } = handler;

    public IInboxStore Inbox { get; } = inbox;

    public IUnitOfWork UnitOfWork { get; } = unitOfWork;

    public TenantContext Tenant { get; } = tenant;

    public ScopedEventOrigin Origin { get; } = origin;

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
