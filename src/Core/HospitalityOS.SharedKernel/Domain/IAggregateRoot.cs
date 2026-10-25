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
    /// Returns the raised events and clears them, so a retried save does not publish them twice (P8).
    /// </summary>
    /// <returns>The events, in the order raised.</returns>
    IReadOnlyList<IDomainEvent> DequeueDomainEvents();
}
