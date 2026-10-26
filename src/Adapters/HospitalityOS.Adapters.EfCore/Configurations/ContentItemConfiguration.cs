using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Properties.ContentItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> builder)
    {
        builder.MapAggregate("ContentItems");
        builder.Property(item => item.PropertyId);
        builder.HasIndex(item => item.PropertyId);
        builder.Property(item => item.Status);
        builder.ComplexProperty(item => item.Details, details =>
        {
            details.Property(value => value.About).HasJsonConversion();
            details.Property(value => value.Tags).HasJsonConversion();
        });
    }
}
