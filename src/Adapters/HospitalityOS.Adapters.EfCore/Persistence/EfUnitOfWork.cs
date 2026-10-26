using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// Commits the scope's <see cref="HospitalityDbContext"/> in one transaction. EF Core tracks the changes; this
/// class also remembers the order aggregates were first seen, so the receipt lists events predictably, and turns
/// the database's concurrency and unique-key failures into conflicts. Scoped.
/// </summary>
internal sealed class EfUnitOfWork(HospitalityDbContext context, IDatabaseErrorClassifier errorClassifier) : IUnitOfWork
{
    private readonly Dictionary<IAggregateRoot, TrackedAggregate> trackedByInstance = new(ReferenceEqualityComparer.Instance);
    private readonly List<TrackedAggregate> trackedInOrder = [];

    public void TrackAdded(Guid id, TenantId tenantId, IAggregateRoot aggregate) => Track(new TrackedAggregate(id, tenantId, aggregate, committedVersion: null));

    public void TrackLoaded(Guid id, TenantId tenantId, IAggregateRoot aggregate)
    {
        if (!trackedByInstance.ContainsKey(aggregate))
        {
            Track(new TrackedAggregate(id, tenantId, aggregate, aggregate.Version));
        }
    }

    public async Task<Result<CommitReceipt>> CommitAsync(CancellationToken cancellationToken)
    {
        List<TrackedAggregate> changed = [.. trackedInOrder.Where(tracked => tracked.HasChanges)];

        if (changed.Count == 0)
        {
            return CommitReceipt.Empty;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return ConcurrencyConflict(exception);
        }
        catch (DbUpdateException exception) when (errorClassifier.IsUniqueViolation(exception, out string? table, out string? column))
        {
            return PersistenceErrors.DuplicateKey(AggregateTypeOf(table), column is null ? "key" : KeyColumns.Describe(column));
        }

        // Only after the database accepted everything, so a failed commit leaves the events in place for a retry.
        List<CommittedEvent> events = [];

        foreach (TrackedAggregate tracked in changed)
        {
            IReadOnlyList<IDomainEvent> raised = tracked.Aggregate.DequeueDomainEvents();
            long firstVersion = tracked.Aggregate.Version - raised.Count + 1;

            events.AddRange(raised.Select((domainEvent, index) => new CommittedEvent(
                tracked.TenantId,
                (tracked.Aggregate as IPropertyScoped)?.PropertyId,
                tracked.Aggregate.GetType().Name,
                tracked.Id,
                firstVersion + index,
                domainEvent)));

            tracked.CommittedVersion = tracked.Aggregate.Version;
        }

        return new CommitReceipt(events);
    }

    private Error ConcurrencyConflict(DbUpdateConcurrencyException exception)
    {
        object? entity = exception.Entries.Count > 0 ? exception.Entries[0].Entity : null;
        TrackedAggregate? tracked = entity is IAggregateRoot aggregate ? trackedByInstance.GetValueOrDefault(aggregate) : null;

        return PersistenceErrors.ConcurrencyConflict(entity?.GetType().Name ?? "Aggregate", tracked?.Id ?? Guid.Empty);
    }

    private string AggregateTypeOf(string? table)
    {
        IEntityType? entityType = table is null ? null : context.Model.GetEntityTypes().FirstOrDefault(type => type.GetTableName() == table);

        return entityType?.ClrType.Name ?? "aggregate";
    }

    private void Track(TrackedAggregate tracked)
    {
        trackedByInstance.Add(tracked.Aggregate, tracked);
        trackedInOrder.Add(tracked);
    }
}
