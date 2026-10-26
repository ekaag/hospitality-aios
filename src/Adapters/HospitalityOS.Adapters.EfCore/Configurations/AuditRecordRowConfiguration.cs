using HospitalityOS.Adapters.EfCore.Auditing;
using HospitalityOS.Ports.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

/// <summary>
/// The audit log: append-only, ordered by a database-assigned position, one row per audit ID, read per tenant.
/// </summary>
internal sealed class AuditRecordRowConfiguration : IEntityTypeConfiguration<AuditRecordRow>
{
    public void Configure(EntityTypeBuilder<AuditRecordRow> builder)
    {
        builder.ToTable("AuditLog");
        builder.HasKey(row => row.Position);
        builder.Property(row => row.Position).ValueGeneratedOnAdd();
        builder.HasIndex(row => row.AuditId).IsUnique();
        builder.HasIndex(row => new { row.TenantId, row.Position });
        builder.HasIndex(row => row.CorrelationId);
        builder.Property(row => row.Action).HasMaxLength(AuditRecord.MaxActionLength);

        // AuditOutcome lives in Ports, outside the scanned domain assemblies, so its name conversion is explicit.
        builder.Property(row => row.Outcome).HasConversion<string>().HasMaxLength(64);
        builder.Property(row => row.ActorId).HasMaxLength(AuditRecord.MaxActorIdLength);
        builder.Property(row => row.TargetType).HasMaxLength(AuditRecord.MaxTargetTypeLength);
        builder.Property(row => row.TargetId).HasMaxLength(AuditRecord.MaxTargetIdLength);
        builder.Property(row => row.Detail).HasMaxLength(AuditRecord.MaxDetailLength);
    }
}
