using HospitalityOS.Adapters.InMemory.Persistence;
using HospitalityOS.Domain.Guests.GuestLinks;

namespace HospitalityOS.Adapters.InMemory.Repositories;

/// <summary>
/// A read past the tenant filter (see <see cref="IGuestLinkDirectory"/>), over committed links only.
/// </summary>
internal sealed class InMemoryGuestLinkDirectory(InMemoryStore store) : IGuestLinkDirectory
{
    public Task<GuestLinkEntry?> FindByCodeAsync(string codeHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GuestLink? link = store.ListAllTenants(typeof(GuestLink))
            .Select(stored => (GuestLink)stored.Snapshot)
            .FirstOrDefault(link => link.CodeHash == codeHash && link.RevokedAt is null);

        return Task.FromResult(link is null ? null : new GuestLinkEntry(link.TenantId, link.Id));
    }
}
