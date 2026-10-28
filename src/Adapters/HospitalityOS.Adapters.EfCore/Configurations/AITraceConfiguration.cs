using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Capabilities.AITraces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class AITraceConfiguration : IEntityTypeConfiguration<AITrace>
{
    public void Configure(EntityTypeBuilder<AITrace> builder)
    {
        builder.MapAggregate("AITraces");
        builder.Property(trace => trace.PropertyId);
        builder.Property(trace => trace.ActorType);
        builder.Property(trace => trace.ActorId).HasMaxLength(AITrace.MaxActorIdLength);
        builder.Property(trace => trace.CorrelationId);
        builder.Property(trace => trace.CausationId);
        builder.Property(trace => trace.TriggerType).HasMaxLength(AITrace.MaxTriggerTypeLength);
        builder.Property(trace => trace.TriggerId);
        builder.Property(trace => trace.Intent).HasMaxLength(AITrace.MaxIntentLength);
        builder.Property(trace => trace.Outcome);
        builder.Property(trace => trace.StartedAt);
        builder.Property(trace => trace.CompletedAt);
        builder.Property(trace => trace.Body);
        builder.Ignore(trace => trace.Actor);
        builder.Property<string>(KeyColumns.RecencyKey).HasMaxLength(KeyColumns.RecencyKeyLength).IsRequired();
        builder.HasIndex(nameof(AITrace.TenantId), KeyColumns.RecencyKey);
        builder.HasIndex(trace => trace.CorrelationId);
    }
}
