namespace HospitalityOS.Seeding;

/// <summary>
/// Puts the reference property, the Aurora Grand (<see cref="AuroraGrand"/>), in place: configured through the same
/// administration services an administrator uses (so it is validated, audited, and raises the same events), with fixed
/// IDs. Idempotent: records that exist are left as they are, so running it again creates nothing. Scoped; run it in a
/// scope whose tenant is <see cref="AuroraGrand.TenantId"/>.
/// </summary>
public interface IReferenceDataSeeder
{
    /// <summary>
    /// Creates whatever of the Aurora Grand does not exist yet. Seed data is the platform's own, so a refusal is a
    /// defect and throws <see cref="InvalidOperationException"/>, as does running outside the Aurora Grand's tenant.
    /// </summary>
    /// <returns>What was created, what existed, and the PMS stay import's outcome.</returns>
    Task<SeedReport> SeedAuroraGrandAsync(CancellationToken cancellationToken);
}
