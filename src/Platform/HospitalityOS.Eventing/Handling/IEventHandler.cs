using System.Diagnostics.CodeAnalysis;
using HospitalityOS.Contracts.Events;

namespace HospitalityOS.Eventing.Handling;

/// <summary>
/// Reacts to one published event type (P1): controllers and workflows implement it and register it with
/// <c>AddEventHandler</c> under a stable name. The platform makes it idempotent (P8): it runs in its own scope for the
/// event's tenant, is skipped when already processed, and its changes are committed together with the record that it
/// processed the event, so redelivered and replayed events cause their effect once (Journey 19).
/// </summary>
/// <remarks>
/// Do not commit: the platform commits after the handler returns. Throwing asks for redelivery (and eventually a
/// dead letter), so throw only for failures worth retrying. Events the handler raises continue the handled event's
/// flow: same correlation, caused by it.
/// </remarks>
/// <typeparam name="TPayload">The published payload version handled, e.g. <c>HumanActionCompletedV1</c>.</typeparam>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Handles platform events (spec §8), not .NET event delegates; 'event handler' is the plan's and spec's term.")]
public interface IEventHandler<in TPayload>
    where TPayload : IEventPayload
{
    /// <summary>
    /// Handles one event.
    /// </summary>
    /// <param name="envelope">The full envelope (tenant, property, correlation, actor).</param>
    /// <param name="payload">The typed payload.</param>
    /// <param name="cancellationToken">Cancels handling; nothing is committed.</param>
    Task HandleAsync(EventEnvelope envelope, TPayload payload, CancellationToken cancellationToken);
}
