using System.Text.Json;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Scheduling;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Eventing.Scheduling;

/// <summary>
/// A scheduled handler's name and input type, registered by <c>AddScheduledHandler</c>.
/// </summary>
internal sealed record ScheduledHandlerRegistration(string Name, Type PayloadType);

/// <summary>
/// Runs one scheduled handler on a message's payload.
/// </summary>
internal interface IScheduledHandlerInvoker
{
    Task InvokeAsync(string payload, CancellationToken cancellationToken);
}

internal sealed class ScheduledHandlerInvoker<TPayload, THandler>(THandler handler) : IScheduledHandlerInvoker
    where THandler : IScheduledHandler<TPayload>
{
    public Task InvokeAsync(string payload, CancellationToken cancellationToken) =>
        handler.HandleAsync(
            JsonSerializer.Deserialize<TPayload>(payload, ContractJson.Options) ?? throw new JsonException("The scheduled payload is null."),
            cancellationToken);
}

internal sealed class WorkScheduler(
    IEnumerable<ScheduledHandlerRegistration> registrations,
    IBindingResolver<IScheduler> schedulers,
    IBindingResolver<IIdGenerator> idGenerators,
    ICurrentTenant currentTenant,
    OperationContext operation) : IWorkScheduler
{
    public Task ScheduleAsync<TPayload>(string handler, TPayload payload, DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!registrations.Any(registration => registration.Name == handler && registration.PayloadType == typeof(TPayload)))
        {
            throw new InvalidOperationException($"No scheduled handler '{handler}' takes {typeof(TPayload).Name}.");
        }

        return schedulers.Resolve().ScheduleAsync(
            new ScheduledMessage(
                idGenerators.Resolve().NewId(),
                handler,
                currentTenant.TenantId,
                operation.CorrelationId,
                operation.CausationId,
                JsonSerializer.Serialize(payload, ContractJson.Options),
                dueAt),
            cancellationToken);
    }
}
