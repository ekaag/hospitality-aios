using HospitalityOS.Domain.Properties.Spaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> builder)
    {
        builder.MapAggregate("Spaces");
        builder.Property(space => space.PropertyId);
        builder.HasIndex(space => space.PropertyId);
        builder.Property(space => space.ParentSpaceId);
        builder.ComplexProperty(space => space.Details);
    }
}
