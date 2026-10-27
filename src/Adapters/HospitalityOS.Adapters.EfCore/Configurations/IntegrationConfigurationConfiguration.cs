using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class IntegrationConfigurationConfiguration : IEntityTypeConfiguration<IntegrationConfiguration>
{
    public void Configure(EntityTypeBuilder<IntegrationConfiguration> builder)
    {
        builder.MapAggregate("IntegrationConfigurations");
        builder.Property(configuration => configuration.PropertyId);
        builder.Property(configuration => configuration.Kind);
        builder.Property(configuration => configuration.ProviderName).HasMaxLength(IntegrationConfiguration.MaxProviderNameLength);
        builder.Property(configuration => configuration.Settings).HasJsonConversion();
        builder.Property(configuration => configuration.Status);
        builder.HasIndex(configuration => new { configuration.PropertyId, configuration.Kind }).IsUnique();
    }
}
