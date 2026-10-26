using HospitalityOS.Application.Auditing;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// The DI scope one attempt runs in, apart from the caller's, so committing it never commits the caller's own staged
/// work, and a failed attempt's changes are discarded with it. <see cref="Enter"/> carries the caller's tenant, flow,
/// and actor across.
/// </summary>
internal sealed class CapabilityScope(
    Func<CapabilityDefinition, ICapabilityInvoker> invokers,
    TenantContext tenant,
    OperationContext operation,
    IUnitOfWork unitOfWork,
    ICapabilityInvocationRepository invocations,
    IAuditTrail audit,
    AsyncServiceScope owner) : IAsyncDisposable
{
    public IUnitOfWork UnitOfWork { get; } = unitOfWork;

    public ICapabilityInvocationRepository Invocations { get; } = invocations;

    public IAuditTrail Audit { get; } = audit;

    public CapabilityScope Enter(TenantId tenantId, Guid correlationId, Guid? causationId, Actor actor)
    {
        tenant.Set(tenantId);
        operation.JoinFlow(correlationId, causationId);
        operation.ActAs(actor);

        return this;
    }

    /// <summary>
    /// Resolved only after <see cref="Enter"/>, so the capability is built inside the caller's tenant and flow.
    /// </summary>
    public ICapabilityInvoker Invoker(CapabilityDefinition definition) => invokers(definition);

    public ValueTask DisposeAsync() => owner.DisposeAsync();
}
