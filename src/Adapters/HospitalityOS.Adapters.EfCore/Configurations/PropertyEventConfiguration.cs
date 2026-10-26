using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Activities.PropertyEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class PropertyEventConfiguration : IEntityTypeConfiguration<PropertyEvent>
{
    public void Configure(EntityTypeBuilder<PropertyEvent> builder)
    {
        builder.MapAggregate("PropertyEvents");
        builder.Property(propertyEvent => propertyEvent.PropertyId);
        builder.HasIndex(propertyEvent => propertyEvent.PropertyId);
        builder.Property(propertyEvent => propertyEvent.Status);
        builder.ComplexProperty(propertyEvent => propertyEvent.Details, details => details.Property(value => value.Price).HasJsonConversion());
    }
}
