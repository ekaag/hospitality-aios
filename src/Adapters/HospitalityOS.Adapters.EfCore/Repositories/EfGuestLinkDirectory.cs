using HospitalityOS.Adapters.EfCore.Persistence;
using HospitalityOS.Domain.Guests.GuestLinks;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Repositories;

/// <summary>
/// A query past the tenant filter (see <see cref="IGuestLinkDirectory"/>): untracked, by a unique key, and answering
/// with no more than which link in which tenant.
/// </summary>
internal sealed class EfGuestLinkDirectory(HospitalityDbContext context) : IGuestLinkDirectory
{
    public async Task<GuestLinkEntry?> FindByCodeAsync(string codeHash, CancellationToken cancellationToken)
    {
        GuestLink? link = await context.Set<GuestLink>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(link => link.CodeHash == codeHash && link.RevokedAt == null)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return link is null ? null : new GuestLinkEntry(link.TenantId, link.Id);
    }
}
