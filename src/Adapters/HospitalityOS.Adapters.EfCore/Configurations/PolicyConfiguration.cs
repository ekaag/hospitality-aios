using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Properties.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.MapAggregate("Policies");
        builder.Property(policy => policy.PropertyId);
        builder.HasIndex(policy => policy.PropertyId);
        builder.Property(policy => policy.Status);
        builder.ComplexProperty(policy => policy.Details, details => details.Property(value => value.AppliesTo).HasJsonConversion());
    }
}
