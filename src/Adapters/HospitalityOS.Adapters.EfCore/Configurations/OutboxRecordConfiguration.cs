using HospitalityOS.Adapters.EfCore.Eventing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

/// <summary>
/// The publish queue, ordered by a database-assigned sequence, one row per event ID.
/// </summary>
internal sealed class OutboxRecordConfiguration : IEntityTypeConfiguration<OutboxRecord>
{
    public void Configure(EntityTypeBuilder<OutboxRecord> builder)
    {
        builder.ToTable("Outbox");
        builder.HasKey(record => record.Sequence);
        builder.Property(record => record.Sequence).ValueGeneratedOnAdd();
        builder.HasIndex(record => record.EventId).IsUnique();
        builder.Property(record => record.EventType).HasMaxLength(200);
        builder.Property(record => record.AggregateType).HasMaxLength(200);
    }
}
