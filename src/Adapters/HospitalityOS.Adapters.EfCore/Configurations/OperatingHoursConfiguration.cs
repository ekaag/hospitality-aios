using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Properties.Hours;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class OperatingHoursConfiguration : IEntityTypeConfiguration<OperatingHours>
{
    public void Configure(EntityTypeBuilder<OperatingHours> builder)
    {
        builder.MapAggregate("OperatingHours");
        builder.Property(hours => hours.PropertyId);
        builder.ComplexProperty(hours => hours.Subject);
        builder.Property(hours => hours.Schedule).HasJsonConversion();
        builder.Property<string>(KeyColumns.HoursSubjectKey).HasMaxLength(100);
        builder.HasIndex(nameof(OperatingHours.PropertyId), KeyColumns.HoursSubjectKey).IsUnique();
    }
}
