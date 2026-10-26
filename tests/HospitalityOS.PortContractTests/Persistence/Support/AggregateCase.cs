using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <summary>
/// How the generic repository contract drives one aggregate type through its repository.
/// </summary>
internal abstract class AggregateCase
{
    public abstract string Name { get; }

    /// <summary>
    /// Whether the aggregate can change after creation; immutable ones (e.g. guest messages) skip change tests.
    /// </summary>
    public abstract bool CanChange { get; }

    public abstract IAggregateRoot Create(TenantId tenantId, int seed);

    public abstract Guid IdOf(IAggregateRoot aggregate);

    public abstract Task AddAsync(PersistenceSession session, IAggregateRoot aggregate, CancellationToken cancellationToken);

    public abstract Task<IAggregateRoot?> GetAsync(PersistenceSession session, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a change that raises exactly one event.
    /// </summary>
    public abstract void Change(IAggregateRoot aggregate);
}
