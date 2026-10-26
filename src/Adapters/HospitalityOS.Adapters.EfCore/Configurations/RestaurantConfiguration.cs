using HospitalityOS.Domain.Dining.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.MapAggregate("Restaurants");
        builder.Property(restaurant => restaurant.PropertyId);
        builder.HasIndex(restaurant => restaurant.PropertyId);
        builder.ComplexProperty(restaurant => restaurant.Details);
    }
}
