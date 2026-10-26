using HospitalityOS.Domain.Properties.Amenities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class AmenityConfiguration : IEntityTypeConfiguration<Amenity>
{
    public void Configure(EntityTypeBuilder<Amenity> builder)
    {
        builder.MapAggregate("Amenities");
        builder.Property(amenity => amenity.PropertyId);
        builder.HasIndex(amenity => amenity.PropertyId);
        builder.Property(amenity => amenity.LocationSpaceId);
        builder.ComplexProperty(amenity => amenity.Details);
    }
}
