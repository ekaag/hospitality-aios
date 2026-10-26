using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class CapabilityInvocationConfiguration : IEntityTypeConfiguration<CapabilityInvocation>
{
    public void Configure(EntityTypeBuilder<CapabilityInvocation> builder)
    {
        builder.MapAggregate("CapabilityInvocations");
        builder.Property(invocation => invocation.CapabilityName).HasMaxLength(CapabilityInvocation.MaxCapabilityNameLength);
        builder.Property(invocation => invocation.CapabilityVersion);
        builder.Property(invocation => invocation.IdempotencyKey).HasMaxLength(CapabilityInvocation.MaxIdempotencyKeyLength);
        builder.Property(invocation => invocation.InputHash).HasMaxLength(CapabilityInvocation.InputHashLength);
        builder.Property(invocation => invocation.ActorType);
        builder.Property(invocation => invocation.ActorId).HasMaxLength(CapabilityInvocation.MaxActorIdLength);
        builder.Property(invocation => invocation.ActorRole);
        builder.Property(invocation => invocation.CorrelationId);
        builder.Property(invocation => invocation.CausationId);
        builder.Property(invocation => invocation.Status);
        builder.Property(invocation => invocation.Output);
        builder.Property(invocation => invocation.ErrorCode).HasMaxLength(CapabilityInvocation.MaxErrorCodeLength);
        builder.Property(invocation => invocation.ErrorMessage).HasMaxLength(CapabilityInvocation.MaxErrorMessageLength);
        builder.Property(invocation => invocation.RequestedAt);
        builder.Property(invocation => invocation.CompletedAt);
        builder.Ignore(invocation => invocation.Actor);
        builder.Property<string?>(KeyColumns.SucceededIdempotencyKey)
            .HasMaxLength(CapabilityInvocation.MaxCapabilityNameLength + 1 + CapabilityInvocation.MaxIdempotencyKeyLength);
        builder.HasIndex(nameof(CapabilityInvocation.TenantId), KeyColumns.SucceededIdempotencyKey).IsUnique();
        builder.HasIndex(invocation => invocation.CorrelationId);
    }
}
