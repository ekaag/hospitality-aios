using HospitalityOS.Adapters.EfCore.Eventing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

/// <summary>
/// Processed events per handler; the composite key makes a racing duplicate fail its commit.
/// </summary>
internal sealed class InboxRecordConfiguration : IEntityTypeConfiguration<InboxRecord>
{
    public void Configure(EntityTypeBuilder<InboxRecord> builder)
    {
        builder.ToTable("Inbox");
        builder.HasKey(record => new { record.Handler, record.EventId });
        builder.Property(record => record.Handler).HasMaxLength(InboxRecord.MaxHandlerLength);
    }
}
