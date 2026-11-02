using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Seeding;

internal sealed class AuroraGrandSeeder(
    SeedSteps steps,
    AuroraGrandProperty property,
    AuroraGrandOfferings offerings,
    AuroraGrandGuests guests,
    MysoreGrandProperty mysoreProperty,
    MysoreGrandOfferings mysoreOfferings,
    IContentIndexer contentIndex,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider) : IReferenceDataSeeder
{
    public async Task<SeedReport> SeedReferencePropertiesAsync(CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId != AuroraGrand.TenantId)
        {
            throw new InvalidOperationException($"The Aurora Grand is seeded in its own tenant ({AuroraGrand.TenantId}), not {currentTenant.TenantId}.");
        }

        DateOnly localToday = AuroraGrand.LocalToday(timeProvider.GetUtcNow());

        await property.SeedAsync(localToday, cancellationToken).ConfigureAwait(false);
        await offerings.SeedAsync(localToday, cancellationToken).ConfigureAwait(false);
        StayImportReport stayImport = await guests.SeedAsync(localToday, cancellationToken).ConfigureAwait(false);

        // The second property, in the same tenant: no guests, so its integrations are configured without a stay import.
        await mysoreProperty.SeedAsync(cancellationToken).ConfigureAwait(false);
        await mysoreOfferings.SeedAsync(MysoreGrand.LocalToday(timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        await guests.ConfigureIntegrationsAsync(MysoreGrand.PropertyId, cancellationToken).ConfigureAwait(false);

        // Content search is a projection that content events keep current; rebuilding it here makes the seeded content
        // searchable at once, including when a durable store outlives an in-memory index across restarts.
        await IndexAsync("Aurora Grand", AuroraGrand.PropertyId, cancellationToken).ConfigureAwait(false);
        await IndexAsync("Mysore Grand", MysoreGrand.PropertyId, cancellationToken).ConfigureAwait(false);

        return new SeedReport(steps.Created, steps.Existing, stayImport);
    }

    private async Task IndexAsync(string name, PropertyId property, CancellationToken cancellationToken)
    {
        Result<int> indexed = await contentIndex.RebuildAsync(property, cancellationToken).ConfigureAwait(false);

        if (indexed.IsFailure)
        {
            throw new InvalidOperationException($"The {name}'s content could not be indexed: {indexed.Error.Message}");
        }
    }
}
