namespace HospitalityOS.SharedKernel.Domain;

/// <summary>
/// Non-generic view of an aggregate, so persistence can collect raised events without knowing the ID type.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Events raised since the aggregate was loaded or last dequeued, in the order raised.
    /// </summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Data version: 1 after creation, +1 for every recorded change. Grounded AI answers cite it so a trace
    /// names the exact version used (P4, spec Journeys 2 and 16); persistence also uses it for optimistic concurrency.
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Returns the raised events and clears them, so a retried save does not publish them twice (P8).
    /// </summary>
    /// <returns>The events, in the order raised.</returns>
    IReadOnlyList<IDomainEvent> DequeueDomainEvents();
}
