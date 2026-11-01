using HospitalityOS.Eventing.Handling;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Eventing.Failures;

/// <summary>
/// Reads the outbox's dead letters and each handler subscription's, keeping only the tenant's. A handler's failure is
/// retried by removing its dead letter first and then publishing the event again, so a repeat failure is dead-lettered
/// afresh and listed again rather than removed. Scoped.
/// </summary>
internal sealed class FailedEvents(IOutboxStore outbox, IBindingResolver<IEventTransport> transports, IEnumerable<EventHandlerRegistration> registrations) : IFailedEvents
{
    /// <summary>
    /// The most outbox failures listed.
    /// </summary>
    public const int MaxListed = 200;

    /// <summary>
    /// The longest error shown; handler exceptions can be long.
    /// </summary>
    public const int MaxErrorLength = 2000;

    public async Task<IReadOnlyList<FailedEvent>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        List<FailedEvent> failed = [];

        foreach (DeadLetteredOutboxEvent deadLettered in await outbox.ListDeadLettersAsync(tenantId, MaxListed, cancellationToken).ConfigureAwait(false))
        {
            failed.Add(new FailedEvent(deadLettered.Message, null, Bounded(deadLettered.Error), deadLettered.Attempts, deadLettered.DeadLetteredAt));
        }

        IEventTransport transport = transports.Resolve();

        foreach (string handler in Handlers())
        {
            foreach (DeadLetteredEvent deadLettered in await transport.ReadDeadLettersAsync(handler, cancellationToken).ConfigureAwait(false))
            {
                if (deadLettered.Message.TenantId == tenantId)
                {
                    failed.Add(new FailedEvent(deadLettered.Message, handler, Bounded(deadLettered.Reason), deadLettered.DeliveryCount, null));
                }
            }
        }

        return [.. failed.OrderByDescending(failure => failure.FailedAt ?? failure.Message.RecordedAt).ThenBy(failure => failure.Handler, StringComparer.Ordinal)];
    }

    public async Task<bool> RetryAsync(TenantId tenantId, Guid eventId, string? handler, CancellationToken cancellationToken)
    {
        if (handler is null)
        {
            return await outbox.RequeueAsync(tenantId, eventId, cancellationToken).ConfigureAwait(false);
        }

        if (!Handlers().Contains(handler, StringComparer.Ordinal))
        {
            return false;
        }

        IEventTransport transport = transports.Resolve();
        DeadLetteredEvent? deadLettered = (await transport.ReadDeadLettersAsync(handler, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(letter => letter.Message.EventId == eventId && letter.Message.TenantId == tenantId);

        if (deadLettered is null || !await transport.RemoveDeadLetterAsync(handler, eventId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await transport.PublishAsync(deadLettered.Message, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private IEnumerable<string> Handlers() => registrations.Select(registration => registration.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private static string Bounded(string error) => error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
}
