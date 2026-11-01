using HospitalityOS.Domain.Organizations.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class StaffAccessConfiguration : IEntityTypeConfiguration<StaffAccess>
{
    public void Configure(EntityTypeBuilder<StaffAccess> builder)
    {
        builder.MapAggregate("StaffAccess");
        builder.Property(access => access.DisplayName).HasMaxLength(StaffAccess.MaxDisplayNameLength);
        builder.Property(access => access.Role);
        builder.Property(access => access.StaffMemberId);
        builder.Property(access => access.InviteCodeHash).HasMaxLength(StaffAccess.InviteCodeHashLength);
        builder.Property(access => access.InviteExpiresAt);
        builder.Property(access => access.SignInIssuer).HasMaxLength(StaffAccess.MaxIssuerLength);
        builder.Property(access => access.SignInSubject).HasMaxLength(StaffAccess.MaxSubjectLength);
        builder.Property(access => access.LinkedAt);
        builder.Property(access => access.RevokedAt);
        builder.Ignore(access => access.Status);

        // Across tenants: a sign-in acts as one person in one tenant, an invite code opens one access, and a staff member
        // has one access. Unset values (NULL) never collide.
        builder.HasIndex(access => new { access.SignInIssuer, access.SignInSubject }).IsUnique();
        builder.HasIndex(access => access.InviteCodeHash).IsUnique();
        builder.HasIndex(access => access.StaffMemberId).IsUnique();
    }
}
