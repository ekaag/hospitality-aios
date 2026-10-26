using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Dining.Menus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.MapAggregate("Menus");
        builder.Property(menu => menu.PropertyId);
        builder.Property(menu => menu.RestaurantId);
        builder.HasIndex(menu => menu.RestaurantId);
        builder.ComplexProperty(menu => menu.Details, details => details.Property(value => value.Items).HasJsonConversion());
    }
}
