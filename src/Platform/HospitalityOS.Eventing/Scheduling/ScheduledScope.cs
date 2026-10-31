using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Eventing.Scheduling;

/// <summary>
/// The scope one scheduled message runs in: its handler, inbox, unit of work, tenant, and operation.
/// </summary>
internal sealed class ScheduledScope(
    Func<IScheduledHandlerInvoker> handler,
    IInboxStore inbox,
    IUnitOfWork unitOfWork,
    TenantContext tenant,
    OperationContext operation,
    IAsyncDisposable owner) : IAsyncDisposable
{
    public Func<IScheduledHandlerInvoker> Handler { get; } = handler;

    public IInboxStore Inbox { get; } = inbox;

    public IUnitOfWork UnitOfWork { get; } = unitOfWork;

    public TenantContext Tenant { get; } = tenant;

    public OperationContext Operation { get; } = operation;

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
