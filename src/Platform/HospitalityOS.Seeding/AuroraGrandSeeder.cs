using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.Ports.Tenancy;

namespace HospitalityOS.Seeding;

internal sealed class AuroraGrandSeeder(
    SeedSteps steps,
    AuroraGrandProperty property,
    AuroraGrandOfferings offerings,
    AuroraGrandGuests guests,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider) : IReferenceDataSeeder
{
    public async Task<SeedReport> SeedAuroraGrandAsync(CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId != AuroraGrand.TenantId)
        {
            throw new InvalidOperationException($"The Aurora Grand is seeded in its own tenant ({AuroraGrand.TenantId}), not {currentTenant.TenantId}.");
        }

        DateOnly localToday = AuroraGrand.LocalToday(timeProvider.GetUtcNow());

        await property.SeedAsync(localToday, cancellationToken).ConfigureAwait(false);
        await offerings.SeedAsync(localToday, cancellationToken).ConfigureAwait(false);
        StayImportReport stayImport = await guests.SeedAsync(localToday, cancellationToken).ConfigureAwait(false);

        return new SeedReport(steps.Created, steps.Existing, stayImport);
    }
}
