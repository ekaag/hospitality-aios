using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
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
    OperationContext operation,
    IAsyncDisposable owner) : IAsyncDisposable
{
    public IEventHandlerInvoker Handler { get; } = handler;

    public IInboxStore Inbox { get; } = inbox;

    public IUnitOfWork UnitOfWork { get; } = unitOfWork;

    public TenantContext Tenant { get; } = tenant;

    public OperationContext Operation { get; } = operation;

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
