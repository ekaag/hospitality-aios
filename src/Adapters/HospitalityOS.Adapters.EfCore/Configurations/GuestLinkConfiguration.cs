using HospitalityOS.Domain.Guests.GuestLinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class GuestLinkConfiguration : IEntityTypeConfiguration<GuestLink>
{
    public void Configure(EntityTypeBuilder<GuestLink> builder)
    {
        builder.MapAggregate("GuestLinks");
        builder.Property(link => link.PropertyId);
        builder.Property(link => link.StayContextId);
        builder.Property(link => link.GuestId);
        builder.Property(link => link.CodeHash).HasMaxLength(GuestLink.CodeHashLength);
        builder.Property(link => link.CreatedAt);
        builder.Property(link => link.RevokedAt);
        builder.Ignore(link => link.IsOpen);

        // Across tenants: a code opens one link. Revoked links hold no code.
        builder.HasIndex(link => link.CodeHash).IsUnique();
        builder.HasIndex(link => link.StayContextId);
    }
}
