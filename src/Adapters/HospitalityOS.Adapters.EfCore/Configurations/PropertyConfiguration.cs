using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Properties.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.MapAggregate("Properties");
        builder.Property(property => property.OrganizationId);
        builder.ComplexProperty(property => property.Details, details => details.Property(value => value.Address).HasJsonConversion());
    }
}
