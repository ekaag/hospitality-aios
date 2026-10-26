using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <inheritdoc />
internal sealed class AggregateCase<TAggregate, TId, TRepository>(
    Func<TenantId, int, TAggregate> create,
    Func<Guid, TId> toId,
    Func<TRepository, TAggregate, CancellationToken, Task> add,
    Func<TRepository, TId, CancellationToken, Task<TAggregate?>> get,
    Func<TAggregate, Result>? change) : AggregateCase
    where TAggregate : AggregateRoot<TId>
    where TId : struct, IEquatable<TId>, ITypedId
    where TRepository : notnull
{
    public override string Name => typeof(TAggregate).Name;

    public override bool CanChange => change is not null;

    public override IAggregateRoot Create(TenantId tenantId, int seed) => create(tenantId, seed);

    public override Guid IdOf(IAggregateRoot aggregate) => ((TAggregate)aggregate).Id.Value;

    public override Task AddAsync(PersistenceSession session, IAggregateRoot aggregate, CancellationToken cancellationToken) =>
        add(session.Services.GetRequiredService<TRepository>(), (TAggregate)aggregate, cancellationToken);

    public override async Task<IAggregateRoot?> GetAsync(PersistenceSession session, Guid id, CancellationToken cancellationToken) =>
        await get(session.Services.GetRequiredService<TRepository>(), toId(id), cancellationToken);

    public override void Change(IAggregateRoot aggregate)
    {
        if (change is null)
        {
            throw new InvalidOperationException($"{Name} cannot change.");
        }

        long before = aggregate.Version;
        Result result = change((TAggregate)aggregate);

        if (result.IsFailure || aggregate.Version != before + 1)
        {
            throw new InvalidOperationException($"The {Name} test change must succeed and raise one event.");
        }
    }
}
