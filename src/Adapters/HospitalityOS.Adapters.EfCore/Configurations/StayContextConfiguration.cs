using HospitalityOS.Domain.Guests.StayContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class StayContextConfiguration : IEntityTypeConfiguration<StayContext>
{
    public void Configure(EntityTypeBuilder<StayContext> builder)
    {
        builder.MapAggregate("StayContexts");
        builder.Property(stay => stay.PropertyId);
        builder.Property(stay => stay.GuestId);
        builder.HasIndex(stay => new { stay.PropertyId, stay.GuestId });
        builder.Property(stay => stay.Arrival);
        builder.Property(stay => stay.Departure);
        builder.Property(stay => stay.AccommodationUnitId);
        builder.Property(stay => stay.Status);
    }
}
