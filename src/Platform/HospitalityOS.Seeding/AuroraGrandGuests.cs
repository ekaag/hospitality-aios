using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.IntegrationRuntime.Configuration;
using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Seeding;

/// <summary>
/// The Aurora Grand's integrations and its in-house guest: the mock PMS, native CMS, and mock messaging are configured;
/// Alex's stay in room 412 is created with its fixed IDs and checked in; then the PMS import links Alex's reservation
/// to it, as it would for a stay created before the property connected its PMS (Journey 18).
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
        foreach ((ConnectorKind kind, string provider) in providers)
        {
            await ConfigureAsync(kind, provider, cancellationToken).ConfigureAwait(false);
        }

        await SeedAlexAsync(localToday, cancellationToken).ConfigureAwait(false);

        return await ImportStaysAsync(cancellationToken).ConfigureAwait(false);
    }

    // A property that already has a connector of this kind keeps it, even if an administrator chose another provider.
    private async Task ConfigureAsync(ConnectorKind kind, string provider, CancellationToken cancellationToken)
    {
        if (await configurations.FindForPropertyAsync(AuroraGrand.PropertyId, kind, cancellationToken).ConfigureAwait(false) is not null)
        {
            steps.CountExisting();
            return;
        }

        Result<IntegrationConfigurationId> configured = await configurator
            .ConfigureAsync(AuroraGrand.PropertyId, kind, provider, new Dictionary<string, string>(), cancellationToken)
            .ConfigureAwait(false);

        if (configured.IsFailure)
        {
            throw SeedSteps.Failed($"{kind} integration", configured.Error);
        }

        await CommitAsync(
            "IntegrationConfiguration.Configure",
            new AuditTarget(nameof(IntegrationConfiguration), configured.Value.Value.ToString(), AuroraGrand.PropertyId),
            cancellationToken).ConfigureAwait(false);
        steps.CountCreated();
    }

    private async Task SeedAlexAsync(DateOnly localToday, CancellationToken cancellationToken)
    {
        if (await stays.GetByIdAsync(AuroraGrand.AlexStay, cancellationToken).ConfigureAwait(false) is not null)
        {
            steps.CountExisting();
            return;
        }

        if (await stays.FindInHouseInUnitAsync(AuroraGrand.PropertyId, AuroraGrand.Room412, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Seeding Alex's stay failed: room 412 is occupied by another in-house stay.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guest? alex = await guests.GetByIdAsync(AuroraGrand.Alex, cancellationToken).ConfigureAwait(false);

        if (alex is null)
        {
            alex = Required("guest Alex", Guest.Register(AuroraGrand.Alex, AuroraGrand.TenantId, new GuestProfile("Alex Morgan", "en"), now));
            await guests.AddAsync(alex, cancellationToken).ConfigureAwait(false);
            auditTrail.Record("Guest.Register", AuditOutcome.Succeeded, new AuditTarget(nameof(Guest), AuroraGrand.Alex.ToString()));
        }

        StayContext stay = Required(
            "Alex's stay",
            StayContext.Create(AuroraGrand.AlexStay, alex, AuroraGrand.PropertyId, localToday, AuroraGrand.AlexDeparture(localToday), AuroraGrand.Room412, now));
        Required("checking Alex in", stay.CheckIn(null, now));
        await stays.AddAsync(stay, cancellationToken).ConfigureAwait(false);
        await CommitAsync("StayContext.Create", new AuditTarget(nameof(StayContext), AuroraGrand.AlexStay.ToString(), AuroraGrand.PropertyId), cancellationToken).ConfigureAwait(false);
        steps.CountCreated();
    }

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
