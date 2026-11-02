using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationRuntime.Configuration;
using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Seeding;

/// <summary>
/// The reference hotels' integrations and in-house guests: the mock PMS, native CMS, and mock messaging are configured;
/// Alex's stay in room 412 is created with its fixed IDs and checked in; then the PMS import links Alex's reservation
/// to it, as it would for a stay created before the property connected its PMS (Journey 18). The Mysore Grand's guest,
/// Ananya, is seeded the same way, without a PMS reservation.
/// </summary>
internal sealed class AuroraGrandGuests(
    SeedSteps steps,
    IIntegrationConfigurator configurator,
    IIntegrationConfigurationRepository configurations,
    IGuestRepository guests,
    IStayContextRepository stays,
    IPmsStayImport stayImport,
    IAuditTrail auditTrail,
    AuditedCommit commit,
    TimeProvider timeProvider)
{
    // Provider names as the providers register them (mock PMS and messaging are adapters, which the seed cannot
    // reference); a host without them fails the seed with integration.provider_unknown.
    private static readonly (ConnectorKind Kind, string Provider)[] providers =
    [
        (ConnectorKind.Pms, "MockPms"),
        (ConnectorKind.Cms, "NativeCms"),
        (ConnectorKind.Messaging, "MockMessaging"),
    ];

    public async Task<StayImportReport> SeedAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        await ConfigureIntegrationsAsync(AuroraGrand.PropertyId, cancellationToken).ConfigureAwait(false);
        await SeedInHouseAsync(
            new InHouseGuest(AuroraGrand.Alex, AuroraGrand.AlexStay, AuroraGrand.PropertyId, AuroraGrand.Room412, "412", "Alex Morgan", localToday, AuroraGrand.AlexDeparture(localToday)),
            cancellationToken).ConfigureAwait(false);

        return await ImportStaysAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Configures the mock PMS, native CMS, and mock messaging for a seeded property (the Aurora Grand's, and the Mysore
    /// Grand's).
    /// </summary>
    public async Task ConfigureIntegrationsAsync(PropertyId property, CancellationToken cancellationToken)
    {
        foreach ((ConnectorKind kind, string provider) in providers)
        {
            await ConfigureAsync(property, kind, provider, cancellationToken).ConfigureAwait(false);
        }
    }

    // A property that already has a connector of this kind keeps it, even if an administrator chose another provider.
    private async Task ConfigureAsync(PropertyId property, ConnectorKind kind, string provider, CancellationToken cancellationToken)
    {
        if (await configurations.FindForPropertyAsync(property, kind, cancellationToken).ConfigureAwait(false) is not null)
        {
            steps.CountExisting();
            return;
        }

        Result<IntegrationConfigurationId> configured = await configurator
            .ConfigureAsync(property, kind, provider, new Dictionary<string, string>(), cancellationToken)
            .ConfigureAwait(false);

        if (configured.IsFailure)
        {
            throw SeedSteps.Failed($"{kind} integration", configured.Error);
        }

        await CommitAsync(
            "IntegrationConfiguration.Configure",
            new AuditTarget(nameof(IntegrationConfiguration), configured.Value.Value.ToString(), property),
            cancellationToken).ConfigureAwait(false);
        steps.CountCreated();
    }

    /// <summary>
    /// Seeds the Mysore Grand's in-house guest, Ananya, in room 210 from <paramref name="localToday"/> for two nights.
    /// </summary>
    public Task SeedMysoreGrandGuestAsync(DateOnly localToday, CancellationToken cancellationToken) =>
        SeedInHouseAsync(
            new InHouseGuest(MysoreGrand.Ananya, MysoreGrand.AnanyaStay, MysoreGrand.PropertyId, MysoreGrand.Room210, "210", "Ananya Rao", localToday, localToday.AddDays(2)),
            cancellationToken);

    private async Task SeedInHouseAsync(InHouseGuest seeded, CancellationToken cancellationToken)
    {
        if (await stays.GetByIdAsync(seeded.Stay, cancellationToken).ConfigureAwait(false) is not null)
        {
            steps.CountExisting();
            return;
        }

        if (await stays.FindInHouseInUnitAsync(seeded.Property, seeded.Unit, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException($"Seeding {seeded.Name}'s stay failed: room {seeded.Room} is occupied by another in-house stay.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guest? guest = await guests.GetByIdAsync(seeded.Guest, cancellationToken).ConfigureAwait(false);

        if (guest is null)
        {
            guest = Required($"guest {seeded.Name}", Guest.Register(seeded.Guest, AuroraGrand.TenantId, new GuestProfile(seeded.Name, "en"), now));
            await guests.AddAsync(guest, cancellationToken).ConfigureAwait(false);
            auditTrail.Record("Guest.Register", AuditOutcome.Succeeded, new AuditTarget(nameof(Guest), seeded.Guest.ToString()));
        }

        StayContext stay = Required(
            $"{seeded.Name}'s stay",
            StayContext.Create(seeded.Stay, guest, seeded.Property, seeded.Arrival, seeded.Departure, seeded.Unit, now));
        Required($"checking {seeded.Name} in", stay.CheckIn(null, now));
        await stays.AddAsync(stay, cancellationToken).ConfigureAwait(false);
        await CommitAsync("StayContext.Create", new AuditTarget(nameof(StayContext), seeded.Stay.ToString(), seeded.Property), cancellationToken).ConfigureAwait(false);
        steps.CountCreated();
    }

    private sealed record InHouseGuest(
        GuestId Guest,
        StayContextId Stay,
        PropertyId Property,
        AccommodationUnitId Unit,
        string Room,
        string Name,
        DateOnly Arrival,
        DateOnly Departure);

    // The import is idempotent; it is committed (and audited) only when it changed something.
    private async Task<StayImportReport> ImportStaysAsync(CancellationToken cancellationToken)
    {
        Result<StayImportReport> imported = await stayImport.ImportInHouseStaysAsync(AuroraGrand.PropertyId, cancellationToken).ConfigureAwait(false);

        if (imported.IsFailure)
        {
            throw SeedSteps.Failed("the PMS stay import", imported.Error);
        }

        if (imported.Value.Imported.Any(stay => stay.Outcome != StayImportOutcome.Unchanged))
        {
            await CommitAsync("PmsStay.Import", new AuditTarget("Property", AuroraGrand.PropertyId.ToString(), AuroraGrand.PropertyId), cancellationToken).ConfigureAwait(false);
        }

        return imported.Value;
    }

    private async Task CommitAsync(string action, AuditTarget target, CancellationToken cancellationToken)
    {
        Result committed = await commit.SucceedAsync(action, target, cancellationToken).ConfigureAwait(false);

        if (committed.IsFailure)
        {
            throw SeedSteps.Failed(action, committed.Error);
        }
    }

    private static T Required<T>(string record, Result<T> result) => result.IsSuccess ? result.Value : throw SeedSteps.Failed(record, result.Error);

    private static void Required(string record, Result result)
    {
        if (result.IsFailure)
        {
            throw SeedSteps.Failed(record, result.Error);
        }
    }
}
