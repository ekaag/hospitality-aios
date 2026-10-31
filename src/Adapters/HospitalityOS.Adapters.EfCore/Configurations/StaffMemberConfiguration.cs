using HospitalityOS.Adapters.EfCore.Conversions;
using HospitalityOS.Domain.Operations.StaffMembers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.MapAggregate("StaffMembers");
        builder.Property(member => member.PropertyId);
        builder.Property(member => member.IsOnDuty);
        builder.ComplexProperty(member => member.Details, details => details.Property(value => value.Skills).HasJsonConversion());
        builder.HasIndex(nameof(StaffMember.PropertyId));
    }
}
