using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Seeding;

internal sealed class AuroraGrandSeeder(
    SeedSteps steps,
    AuroraGrandProperty property,
    AuroraGrandOfferings offerings,
    AuroraGrandGuests guests,
    IContentIndexer contentIndex,
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

        // Content search is a projection that content events keep current; rebuilding it here makes the seeded content
        // searchable at once, including when a durable store outlives an in-memory index across restarts.
        Result<int> indexed = await contentIndex.RebuildAsync(AuroraGrand.PropertyId, cancellationToken).ConfigureAwait(false);

        if (indexed.IsFailure)
        {
            throw new InvalidOperationException($"The Aurora Grand's content could not be indexed: {indexed.Error.Message}");
        }

        return new SeedReport(steps.Created, steps.Existing, stayImport);
    }
}
