using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Domain.Guests.GuestLinks;

/// <summary>
/// Finds an open link by its code's hash across every tenant, since a guest brings the code before their tenant is
/// known. Not filtered to the current tenant (P10), so it answers with no more than which link in which tenant; the
/// link is then loaded through <see cref="IGuestLinkRepository"/> in that tenant.
/// </summary>
public interface IGuestLinkDirectory
{
    /// <summary>
    /// The open link with the hash, or <c>null</c>.
    /// </summary>
    Task<GuestLinkEntry?> FindByCodeAsync(string codeHash, CancellationToken cancellationToken);
}

/// <summary>
/// Which link a code opens, and in which tenant.
/// </summary>
public sealed record GuestLinkEntry(TenantId TenantId, GuestLinkId LinkId);
