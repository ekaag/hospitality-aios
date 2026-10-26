namespace HospitalityOS.Ports.Eventing;

/// <summary>
/// Delivers published events to subscribers (P1): in-memory locally, Azure Service Bus in the cloud (K2). Every
/// subscription receives every event published while it exists, at least once. Singleton.
/// </summary>
/// <remarks>
/// When a subscription's handler throws, the transport redelivers the event, up to the adapter's maximum delivery
/// count; then the event moves to the subscription's dead letters, where it stays visible (spec §37: dead-letter
/// permanent failures). Handlers de-duplicate with <see cref="IInboxStore"/>.
/// </remarks>
public interface IEventTransport
{
    /// <summary>
    /// Publishes an event to every current subscription.
    /// </summary>
    Task PublishAsync(EventMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Starts delivering events to <paramref name="handler"/> under a durable subscription name; disposing the result
    /// stops delivery.
    /// </summary>
    /// <param name="subscription">The subscription's stable name, unique per transport.</param>
    /// <param name="handler">Processes one event; throwing asks for redelivery.</param>
    /// <param name="cancellationToken">Cancels subscribing.</param>
    Task<IAsyncDisposable> SubscribeAsync(string subscription, Func<EventMessage, CancellationToken, Task> handler, CancellationToken cancellationToken);

    /// <summary>
    /// The events a subscription gave up on, oldest first.
    /// </summary>
    Task<IReadOnlyList<DeadLetteredEvent>> ReadDeadLettersAsync(string subscription, CancellationToken cancellationToken);
}
