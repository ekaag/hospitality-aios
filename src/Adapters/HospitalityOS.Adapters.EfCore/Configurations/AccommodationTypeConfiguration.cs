using HospitalityOS.Domain.Properties.AccommodationTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class AccommodationTypeConfiguration : IEntityTypeConfiguration<AccommodationType>
{
    public void Configure(EntityTypeBuilder<AccommodationType> builder)
    {
        builder.MapAggregate("AccommodationTypes");
        builder.Property(type => type.PropertyId);
        builder.ComplexProperty(type => type.Details);
    }
}
