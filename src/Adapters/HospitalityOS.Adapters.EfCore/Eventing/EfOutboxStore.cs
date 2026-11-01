using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Eventing;

/// <summary>
/// Every operation is a direct database statement (untracked query, <c>ExecuteUpdate</c>, <c>ExecuteDelete</c>), so
/// it takes effect at once and never saves the scope's pending changes. A claim leases each row with a conditional
/// update, so two dispatchers cannot both claim the same event.
/// </summary>
internal sealed class EfOutboxStore(HospitalityDbContext context) : IOutboxStore
{
    private DbSet<OutboxRecord> Outbox => context.Set<OutboxRecord>();

    public async Task<IReadOnlyList<OutboxEntry>> ClaimDueAsync(DateTimeOffset now, int maxCount, TimeSpan lease, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);

        DateTimeOffset leaseUntil = now + lease;
        List<OutboxRecord> candidates = await Due(Outbox.AsNoTracking(), now)
            .OrderBy(record => record.Sequence)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<OutboxEntry> claimed = [];

        foreach (OutboxRecord candidate in candidates)
        {
            Guid eventId = candidate.EventId;
            int leased = await Due(Outbox.Where(record => record.EventId == eventId), now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(record => record.LeaseUntil, leaseUntil), cancellationToken)
                .ConfigureAwait(false);

            // 0 rows: another dispatcher claimed it between the query and the update.
            if (leased == 1)
            {
                claimed.Add(candidate.ToEntry());
            }
        }

        return claimed;
    }

    public Task MarkDispatchedAsync(Guid eventId, CancellationToken cancellationToken) =>
        Outbox.Where(record => record.EventId == eventId).ExecuteDeleteAsync(cancellationToken);

    public Task ScheduleRetryAsync(Guid eventId, DateTimeOffset retryAt, string error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Outbox.Where(record => record.EventId == eventId).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(record => record.Attempts, record => record.Attempts + 1)
                .SetProperty(record => record.LastError, error)
                .SetProperty(record => record.NextAttemptAt, retryAt)
                .SetProperty(record => record.LeaseUntil, (DateTimeOffset?)null),
            cancellationToken);
    }

    public Task DeadLetterAsync(Guid eventId, DateTimeOffset at, string error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);

        return Outbox.Where(record => record.EventId == eventId).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(record => record.LastError, error)
                .SetProperty(record => record.DeadLetteredAt, at)
                .SetProperty(record => record.LeaseUntil, (DateTimeOffset?)null),
            cancellationToken);
    }

    public async Task<IReadOnlyList<DeadLetteredOutboxEvent>> ListDeadLettersAsync(TenantId tenantId, int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        List<OutboxRecord> found = await Outbox.AsNoTracking()
            .Where(record => record.TenantId == tenantId && record.DeadLetteredAt != null)
            .OrderByDescending(record => record.DeadLetteredAt)
            .ThenByDescending(record => record.Sequence)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. found.Select(record => new DeadLetteredOutboxEvent(record.ToEntry().Message, record.Attempts, record.LastError ?? string.Empty, record.DeadLetteredAt!.Value))];
    }

    public async Task<bool> RequeueAsync(TenantId tenantId, Guid eventId, CancellationToken cancellationToken) =>
        await Outbox.Where(record => record.EventId == eventId && record.TenantId == tenantId && record.DeadLetteredAt != null).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(record => record.DeadLetteredAt, (DateTimeOffset?)null)
                .SetProperty(record => record.Attempts, 0)
                .SetProperty(record => record.NextAttemptAt, (DateTimeOffset?)null)
                .SetProperty(record => record.LeaseUntil, (DateTimeOffset?)null),
            cancellationToken).ConfigureAwait(false) == 1;

    private static IQueryable<OutboxRecord> Due(IQueryable<OutboxRecord> records, DateTimeOffset now) =>
        records.Where(record => record.DeadLetteredAt == null
            && (record.NextAttemptAt == null || record.NextAttemptAt <= now)
            && (record.LeaseUntil == null || record.LeaseUntil <= now));
}
