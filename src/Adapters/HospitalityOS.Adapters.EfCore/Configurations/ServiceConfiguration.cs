using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.MapAggregate("Services");
        builder.Property(service => service.PropertyId);
        builder.ComplexProperty(service => service.Details, details => details.Property(value => value.Items).HasJsonConversion());
        builder.Property<string>(KeyColumns.ServiceCodeKey).HasMaxLength(TextRules.MaxCodeLength);
        builder.HasIndex(nameof(Service.PropertyId), KeyColumns.ServiceCodeKey).IsUnique();
    }
}
