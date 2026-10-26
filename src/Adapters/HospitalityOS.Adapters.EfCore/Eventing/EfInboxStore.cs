using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Ports.Eventing;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Eventing;

/// <summary>
/// Records are added to the scope's context and saved by the unit of work with the handler's changes; the
/// composite key turns a racing duplicate into a duplicate-key conflict.
/// </summary>
internal sealed class EfInboxStore(HospitalityDbContext context) : IInboxStore
{
    public Task<bool> HasProcessedAsync(string handler, Guid eventId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);

        return context.Set<InboxRecord>().AsNoTracking().AnyAsync(record => record.Handler == handler && record.EventId == eventId, cancellationToken);
    }

    public void Record(string handler, Guid eventId, DateTimeOffset processedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(handler.Length, InboxRecord.MaxHandlerLength);

        context.Add(new InboxRecord { Handler = handler, EventId = eventId, ProcessedAt = processedAt });
    }
}
