using HospitalityOS.Ports.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HospitalityOS.Adapters.EfCore.Persistence;

/// <summary>
/// Applies the provider's migrations (EF Core takes a lock, so concurrent runs wait rather than collide), then each
/// <see cref="ISchemaAccess"/> the provider registered, e.g. PostgreSQL's grants to the app's own role.
/// </summary>
internal sealed class EfSchemaMigrator(HospitalityDbContext context, IEnumerable<ISchemaAccess> accesses) : ISchemaMigrator
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        foreach (ISchemaAccess access in accesses)
        {
            await access.GrantAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// A provider's step after migrating: letting the app's own sign-in use the schema the migrations made.
/// </summary>
internal interface ISchemaAccess
{
    /// <summary>
    /// Grants the access, idempotently.
    /// </summary>
    Task GrantAsync(CancellationToken cancellationToken);
}
