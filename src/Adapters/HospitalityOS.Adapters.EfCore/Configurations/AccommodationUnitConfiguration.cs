using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class AccommodationUnitConfiguration : IEntityTypeConfiguration<AccommodationUnit>
{
    public void Configure(EntityTypeBuilder<AccommodationUnit> builder)
    {
        builder.MapAggregate("AccommodationUnits");
        builder.Property(unit => unit.PropertyId);
        builder.Property(unit => unit.AccommodationTypeId);
        builder.Property(unit => unit.SpaceId);
        builder.Property(unit => unit.UnitNumber).HasMaxLength(AccommodationUnit.MaxUnitNumberLength);
        builder.Property<string>(KeyColumns.UnitNumberKey).HasMaxLength(AccommodationUnit.MaxUnitNumberLength);
        builder.HasIndex(nameof(AccommodationUnit.PropertyId), KeyColumns.UnitNumberKey).IsUnique();
    }
}
