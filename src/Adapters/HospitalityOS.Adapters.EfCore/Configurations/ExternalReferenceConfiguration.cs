using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class ExternalReferenceConfiguration : IEntityTypeConfiguration<ExternalReference>
{
    public void Configure(EntityTypeBuilder<ExternalReference> builder)
    {
        builder.MapAggregate("ExternalReferences");
        builder.Property(reference => reference.PropertyId);
        builder.Property(reference => reference.ProviderName).HasMaxLength(IntegrationConfiguration.MaxProviderNameLength);
        builder.Property(reference => reference.EntityType).HasMaxLength(ExternalReference.MaxEntityTypeLength);
        builder.Property(reference => reference.ExternalId).HasMaxLength(ExternalReference.MaxExternalIdLength);
        builder.Property(reference => reference.CanonicalId);
        builder.HasIndex(reference => new { reference.PropertyId, reference.ProviderName, reference.EntityType, reference.ExternalId }).IsUnique();
        builder.HasIndex(reference => new { reference.PropertyId, reference.EntityType, reference.CanonicalId });
    }
}
