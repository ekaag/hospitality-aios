using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationRuntime.References;
using HospitalityOS.IntegrationRuntime.Resolution;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using StayStatus = HospitalityOS.Domain.Guests.StayContexts.StayStatus;

namespace HospitalityOS.IntegrationRuntime.Stays;

internal sealed class PmsStayImport(
    IIntegrationResolver<IPmsConnector> pms,
    IExternalReferences references,
    IPropertyRepository properties,
    IAccommodationUnitRepository units,
    IStayContextRepository stays,
    IGuestRepository guests,
    IBindingResolver<IIdGenerator> idGenerators,
    TimeProvider timeProvider) : IPmsStayImport
{
    private const string StayType = nameof(StayContext);
    private const string GuestType = nameof(Guest);

    public async Task<Result<StayImportReport>> ImportInHouseStaysAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        Property? property = await properties.GetByIdAsync(propertyId, cancellationToken).ConfigureAwait(false);

        if (property is null)
        {
            return IntegrationRuntimeErrors.PropertyNotFound;
        }

        Result<ResolvedConnector<IPmsConnector>> resolved = await pms.ResolveAsync(propertyId, cancellationToken).ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return resolved.Error;
        }

        IReadOnlyList<PmsStay> inHouse = await resolved.Value.Connector.ListInHouseStaysAsync(cancellationToken).ConfigureAwait(false);
        Run run = new(property, resolved.Value.ProviderName, timeProvider.GetUtcNow());

        foreach (PmsStay stay in inHouse.OrderBy(stay => stay.Id.Value, StringComparer.Ordinal))
        {
            await ImportAsync(run, stay, cancellationToken).ConfigureAwait(false);
        }

        return new StayImportReport(run.Provider, ValueList.From(run.Imported), ValueList.From(run.Skipped));
    }

    private async Task ImportAsync(Run run, PmsStay pmsStay, CancellationToken cancellationToken)
    {
        if (pmsStay.UnitNumber is null)
        {
            run.Skip(pmsStay, "The PMS has not assigned a unit.");
            return;
        }

        AccommodationUnit? unit = await units.FindByUnitNumberAsync(run.PropertyId, pmsStay.UnitNumber, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            run.Skip(pmsStay, $"Unit '{pmsStay.UnitNumber}' is not a unit of the property.");
            return;
        }

        if (!run.ClaimedUnits.Add(unit.Id))
        {
            run.Skip(pmsStay, $"Another PMS stay in this import is in unit '{unit.UnitNumber}'.");
            return;
        }

        Guid? linkedStayId = await references.FindCanonicalIdAsync(run.PropertyId, run.Provider, StayType, pmsStay.Id, cancellationToken).ConfigureAwait(false);

        if (linkedStayId is { } stayId)
        {
            StayContext? linked = await stays.GetByIdAsync(new StayContextId(stayId), cancellationToken).ConfigureAwait(false);

            if (linked is null)
            {
                run.Skip(pmsStay, "The linked stay no longer exists.");
                return;
            }

            await UpdateAsync(run, pmsStay, linked, unit, matched: false, cancellationToken).ConfigureAwait(false);
            return;
        }

        StayContext? occupant = await stays.FindInHouseInUnitAsync(run.PropertyId, unit.Id, cancellationToken).ConfigureAwait(false);

        if (occupant is null)
        {
            await CreateAsync(run, pmsStay, unit, cancellationToken).ConfigureAwait(false);
        }
        else if (occupant.Arrival == pmsStay.Arrival)
        {
            // The same stay, created natively or imported from the property's previous PMS: keep its canonical ID.
            await UpdateAsync(run, pmsStay, occupant, unit, matched: true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            run.Skip(pmsStay, $"Unit '{unit.UnitNumber}' is occupied by another in-house stay.");
        }
    }

    private async Task CreateAsync(Run run, PmsStay pmsStay, AccommodationUnit unit, CancellationToken cancellationToken)
    {
        Result<(Guest Guest, bool IsNew)> guest = await FindOrRegisterGuestAsync(run, pmsStay.Guest, cancellationToken).ConfigureAwait(false);

        if (guest.IsFailure)
        {
            run.Skip(pmsStay, guest.Error.Message);
            return;
        }

        Result<StayContext> created = StayContext.Create(
            new StayContextId(idGenerators.Resolve().NewId()), guest.Value.Guest, run.PropertyId, pmsStay.Arrival, pmsStay.Departure, unit.Id, run.Now);

        if (created.IsFailure)
        {
            run.Skip(pmsStay, created.Error.Message);
            return;
        }

        StayContext stay = created.Value;
        stay.CheckIn(null, run.Now);

        if (guest.Value.IsNew)
        {
            await guests.AddAsync(guest.Value.Guest, cancellationToken).ConfigureAwait(false);
            await LinkAsync(run, GuestType, pmsStay.Guest.Id, guest.Value.Guest.Id.Value, cancellationToken).ConfigureAwait(false);
        }

        await stays.AddAsync(stay, cancellationToken).ConfigureAwait(false);
        await LinkAsync(run, StayType, pmsStay.Id, stay.Id.Value, cancellationToken).ConfigureAwait(false);
        run.Imported.Add(new ImportedStay(pmsStay.Id, stay.Id, guest.Value.Guest.Id, StayImportOutcome.Created));
    }

    private async Task UpdateAsync(Run run, PmsStay pmsStay, StayContext stay, AccommodationUnit unit, bool matched, CancellationToken cancellationToken)
    {
        if (stay.Status is StayStatus.CheckedOut or StayStatus.Cancelled)
        {
            run.Skip(pmsStay, $"The stay is {stay.Status} on the platform.");
            return;
        }

        if (stay.Status == StayStatus.InHouse && stay.Arrival != pmsStay.Arrival)
        {
            run.Skip(pmsStay, "The PMS reports a different arrival for a stay already checked in.");
            return;
        }

        Guid? linkedGuest = await references.FindCanonicalIdAsync(run.PropertyId, run.Provider, GuestType, pmsStay.Guest.Id, cancellationToken).ConfigureAwait(false);

        if (linkedGuest is { } guestId && guestId != stay.GuestId.Value)
        {
            run.Skip(pmsStay, "The PMS guest is linked to a different guest than the stay's.");
            return;
        }

        Guest? guest = await guests.GetByIdAsync(stay.GuestId, cancellationToken).ConfigureAwait(false);

        if (guest is null)
        {
            run.Skip(pmsStay, "The stay's guest no longer exists.");
            return;
        }

        long version = stay.Version;
        Result changed = stay.ChangeDates(pmsStay.Arrival, pmsStay.Departure, run.Now);

        if (changed.IsFailure)
        {
            run.Skip(pmsStay, changed.Error.Message);
            return;
        }

        // Profiles are validated by the PMS's own rules too; an invalid one is not worth losing the stay over.
        guest.UpdateProfile(MergedProfile(guest.Profile, pmsStay.Guest), run.Now);

        if (stay.Status == StayStatus.Reserved)
        {
            stay.CheckIn(unit.Id, run.Now);
        }
        else
        {
            stay.AssignUnit(unit.Id, run.Now);
        }

        run.GuestsByPmsId.TryAdd(pmsStay.Guest.Id, guest);
        await LinkAsync(run, GuestType, pmsStay.Guest.Id, stay.GuestId.Value, cancellationToken).ConfigureAwait(false);

        if (matched)
        {
            await LinkAsync(run, StayType, pmsStay.Id, stay.Id.Value, cancellationToken).ConfigureAwait(false);
        }

        StayImportOutcome outcome = matched ? StayImportOutcome.Matched : stay.Version == version ? StayImportOutcome.Unchanged : StayImportOutcome.Updated;
        run.Imported.Add(new ImportedStay(pmsStay.Id, stay.Id, stay.GuestId, outcome));
    }

    /// <summary>
    /// The guest the PMS guest is linked to (with the profile brought up to date), or a new one, not yet added.
    /// </summary>
    private async Task<Result<(Guest Guest, bool IsNew)>> FindOrRegisterGuestAsync(Run run, PmsGuest pmsGuest, CancellationToken cancellationToken)
    {
        if (run.GuestsByPmsId.TryGetValue(pmsGuest.Id, out Guest? known))
        {
            return (known, false);
        }

        Guid? linked = await references.FindCanonicalIdAsync(run.PropertyId, run.Provider, GuestType, pmsGuest.Id, cancellationToken).ConfigureAwait(false);
        Guest? existing = linked is { } guestId ? await guests.GetByIdAsync(new GuestId(guestId), cancellationToken).ConfigureAwait(false) : null;

        if (existing is not null)
        {
            existing.UpdateProfile(MergedProfile(existing.Profile, pmsGuest), run.Now);
            run.GuestsByPmsId.Add(pmsGuest.Id, existing);

            return (existing, false);
        }

        Result<Guest> registered = Guest.Register(
            new GuestId(idGenerators.Resolve().NewId()), run.TenantId, MergedProfile(GuestProfile.Anonymous, pmsGuest), run.Now);

        if (registered.IsFailure)
        {
            return registered.Error;
        }

        run.GuestsByPmsId.Add(pmsGuest.Id, registered.Value);

        return (registered.Value, true);
    }

    private async Task LinkAsync(Run run, string entityType, ExternalId externalId, Guid canonicalId, CancellationToken cancellationToken)
    {
        Result<ExternalReference> linked = await references.LinkAsync(run.PropertyId, run.Provider, entityType, externalId, canonicalId, cancellationToken).ConfigureAwait(false);

        // Conflicts were ruled out before anything changed and the property exists, so a failure is a programming error.
        if (linked.IsFailure)
        {
            throw new InvalidOperationException($"Linking {run.Provider} {entityType} '{externalId}' failed: {linked.Error.Message}");
        }
    }

    /// <summary>
    /// The PMS's values where it has them; the platform keeps what the PMS leaves out.
    /// </summary>
    private static GuestProfile MergedProfile(GuestProfile current, PmsGuest pmsGuest) =>
        new(pmsGuest.DisplayName ?? current.DisplayName, pmsGuest.PreferredLanguage ?? current.PreferredLanguage);

    /// <summary>
    /// One import's state. Repository lookups see committed data only, and nothing this import stages is committed until
    /// the caller commits, so the import remembers what it has already done.
    /// </summary>
    private sealed class Run(Property property, string provider, DateTimeOffset now)
    {
        public PropertyId PropertyId => property.Id;

        public SharedKernel.Tenancy.TenantId TenantId => property.TenantId;

        public string Provider => provider;

        public DateTimeOffset Now => now;

        public Dictionary<ExternalId, Guest> GuestsByPmsId { get; } = [];

        public HashSet<AccommodationUnitId> ClaimedUnits { get; } = [];

        public List<ImportedStay> Imported { get; } = [];

        public List<SkippedStay> Skipped { get; } = [];

        public void Skip(PmsStay pmsStay, string reason) => Skipped.Add(new SkippedStay(pmsStay.Id, reason));
    }
}
