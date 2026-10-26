using System.Linq.Expressions;
using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

/// <summary>
/// What every EF Core repository shares: loads through the tenant-filtered <see cref="HospitalityDbContext"/>
/// (one instance per aggregate per scope), and registers what it returns or adds with the unit of work.
/// </summary>
internal abstract class EfRepository<TAggregate, TId>(HospitalityDbContext context, EfUnitOfWork unitOfWork, ICurrentTenant currentTenant)
    where TAggregate : AggregateRoot<TId>, ITenantScoped
    where TId : struct, IEquatable<TId>, ITypedId
{
    protected DbSet<TAggregate> Aggregates => context.Set<TAggregate>();

    public async Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken)
    {
        TAggregate? aggregate = await Aggregates.FindAsync([id], cancellationToken).ConfigureAwait(false);

        if (aggregate is not null)
        {
            unitOfWork.TrackLoaded(aggregate.Id.Value, aggregate.TenantId, aggregate);
        }

        return aggregate;
    }

    public Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        cancellationToken.ThrowIfCancellationRequested();

        if (aggregate.TenantId != currentTenant.TenantId)
        {
            // A caller holding another tenant's aggregate is a programming error, never an expected failure (P10).
            throw new InvalidOperationException($"Cannot add {typeof(TAggregate).Name} {aggregate.Id} of another tenant.");
        }

        Aggregates.Add(aggregate);
        unitOfWork.TrackAdded(aggregate.Id.Value, aggregate.TenantId, aggregate);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs <paramref name="query"/> in the database (committed data, current tenant) and returns tracked instances.
    /// </summary>
    protected async Task<IReadOnlyList<TAggregate>> QueryAsync(
        Func<IQueryable<TAggregate>, IQueryable<TAggregate>> query,
        CancellationToken cancellationToken)
    {
        List<TAggregate> found = await query(Aggregates).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (TAggregate aggregate in found)
        {
            unitOfWork.TrackLoaded(aggregate.Id.Value, aggregate.TenantId, aggregate);
        }

        return found;
    }

    protected async Task<TAggregate?> FindFirstAsync(Expression<Func<TAggregate, bool>> predicate, CancellationToken cancellationToken)
    {
        IReadOnlyList<TAggregate> found = await QueryAsync(aggregates => aggregates.Where(predicate).Take(1), cancellationToken).ConfigureAwait(false);

        return found.Count == 0 ? null : found[0];
    }
}
