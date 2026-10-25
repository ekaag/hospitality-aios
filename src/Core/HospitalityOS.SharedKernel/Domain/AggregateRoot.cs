namespace HospitalityOS.SharedKernel.Domain;

/// <summary>
/// The entry point to a consistency boundary, loaded and saved as a unit. Records the domain
/// events it raises until persistence takes them for the outbox in the same transaction (P1).
/// </summary>
/// <typeparam name="TId">The strongly typed ID.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> domainEvents = [];

    /// <summary>
    /// Creates an aggregate with the given identity.
    /// </summary>
    /// <param name="id">The identity; must not be the default value.</param>
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => domainEvents;

    /// <inheritdoc />
    public long Version { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        IDomainEvent[] dequeued = [.. domainEvents];
        domainEvents.Clear();

        return dequeued;
    }

    /// <summary>
    /// Records a domain event describing a state change this aggregate just made, and advances
    /// <see cref="Version"/>. Raise exactly one event per state change.
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        domainEvents.Add(domainEvent);
        Version++;
    }
}
