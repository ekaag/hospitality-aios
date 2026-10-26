using HospitalityOS.Adapters.EfCore.Eventing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

/// <summary>
/// The event log: append-only, ordered by a database-assigned position, one row per event ID.
/// </summary>
internal sealed class EventRecordConfiguration : IEntityTypeConfiguration<EventRecord>
{
    public void Configure(EntityTypeBuilder<EventRecord> builder)
    {
        builder.ToTable("EventStore");
        builder.HasKey(record => record.Position);
        builder.Property(record => record.Position).ValueGeneratedOnAdd();
        builder.HasIndex(record => record.EventId).IsUnique();
        builder.Property(record => record.EventType).HasMaxLength(200);
        builder.Property(record => record.AggregateType).HasMaxLength(200);
        builder.HasIndex(record => record.CorrelationId);
        builder.HasIndex(record => record.RecordedAt);
    }
}
