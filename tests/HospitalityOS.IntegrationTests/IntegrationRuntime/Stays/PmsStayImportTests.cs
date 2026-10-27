using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.IntegrationRuntime;
using HospitalityOS.IntegrationRuntime.Configuration;
using HospitalityOS.IntegrationRuntime.References;
using HospitalityOS.IntegrationRuntime.Stays;
using HospitalityOS.Integrations.Mock.Pms;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using PmsStayStatus = HospitalityOS.IntegrationContracts.Pms.StayStatus;
using StayStatus = HospitalityOS.Domain.Guests.StayContexts.StayStatus;

namespace HospitalityOS.IntegrationTests.IntegrationRuntime.Stays;

/// <summary>
/// The PMS stay import in the real host, and Journey 18: swapping the PMS provider changes configuration only, while
/// canonical guests and stays keep their IDs and every provider's IDs live in <c>ExternalReference</c>.
/// </summary>
public sealed class PmsStayImportTests : IAsyncLifetime
{
    private static readonly DateOnly arrival = new(2026, 10, 24);
    private static readonly DateOnly departure = new(2026, 10, 28);

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly ScriptedPmsProvider operaCloud = new("OperaCloud");
    private readonly WebApplicationFactory<Program> factory;

    private AccommodationUnit room412 = null!;
    private AccommodationUnit room415 = null!;

    public PmsStayImportTests()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Eventing:Dispatcher:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.Configure<MockPmsOptions>(options => options.Stays[aurora.PropertyId] =
                [
                    Stay("RES-0412", "G-77", "Alex", "412"),
                    Stay("RES-0500", "G-90", "Sam", null, PmsStayStatus.Reserved),
                ]);
                services.AddSingleton<IIntegrationProvider<IPmsConnector>>(operaCloud);
            });
        });
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private PropertyId Property => aurora.PropertyId;

    public async ValueTask InitializeAsync()
    {
        await aurora.SaveAsync(factory.Services, Cancellation);
        await InTenantAsync(async services =>
        {
            Space floor4 = Space.Create(new SpaceId(Guid.CreateVersion7()), aurora.TenantId, Property, null, new SpaceDetails("Floor 4", SpaceKind.Floor), AuroraGrand.Now).Value;
            AccommodationType deluxeKing = AccommodationType.Create(
                new AccommodationTypeId(Guid.CreateVersion7()), aurora.TenantId, Property, new AccommodationTypeDetails("Deluxe King", 2), AuroraGrand.Now).Value;
            room412 = AccommodationUnit.Create(new AccommodationUnitId(Guid.CreateVersion7()), deluxeKing, floor4, "412", AuroraGrand.Now).Value;
            room415 = AccommodationUnit.Create(new AccommodationUnitId(Guid.CreateVersion7()), deluxeKing, floor4, "415", AuroraGrand.Now).Value;
            await services.GetRequiredService<ISpaceRepository>().AddAsync(floor4, Cancellation);
            await services.GetRequiredService<IAccommodationTypeRepository>().AddAsync(deluxeKing, Cancellation);
            await services.GetRequiredService<IAccommodationUnitRepository>().AddAsync(room412, Cancellation);
            await services.GetRequiredService<IAccommodationUnitRepository>().AddAsync(room415, Cancellation);

            return Result.Success();
        });
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task Import_FromMockPms_CreatesACheckedInGuestAndStayInTheUnitAndLinksTheProvidersIds()
    {
        await ConfigurePmsAsync("MockPms");

        StayImportReport report = await ImportAsync();

        ImportedStay imported = Assert.Single(report.Imported);
        Assert.Equal(("MockPms", "RES-0412", StayImportOutcome.Created), (report.ProviderName, imported.PmsStayId.Value, imported.Outcome));
        Assert.Empty(report.Skipped);
        (StayContext stay, Guest guest) = await LoadAsync(imported.StayContextId);
        Assert.Equal((StayStatus.InHouse, room412.Id, arrival, departure), (stay.Status, stay.AccommodationUnitId, stay.Arrival, stay.Departure));
        Assert.Equal((imported.GuestId, "Alex", "en"), (guest.Id, guest.Profile.DisplayName, guest.Profile.PreferredLanguage));
        Assert.Equal(stay.Id.Value, await FindCanonicalAsync("MockPms", nameof(StayContext), "RES-0412"));
        Assert.Equal(guest.Id.Value, await FindCanonicalAsync("MockPms", nameof(Guest), "G-77"));
    }

    [Fact]
    public async Task Import_Again_IsUnchangedAndCreatesNothing()
    {
        await ConfigurePmsAsync("MockPms");
        ImportedStay first = Assert.Single((await ImportAsync()).Imported);

        ImportedStay second = Assert.Single((await ImportAsync()).Imported);

        Assert.Equal((first.StayContextId, first.GuestId, StayImportOutcome.Unchanged), (second.StayContextId, second.GuestId, second.Outcome));
    }

    [Fact]
    public async Task Import_AfterThePmsMovesAndExtendsTheStay_UpdatesTheLinkedStay()
    {
        await ConfigurePmsAsync("OperaCloud");
        operaCloud.Stays = [Stay("OC-1", "OC-G-1", "Alex", "412")];
        ImportedStay first = Assert.Single((await ImportAsync()).Imported);

        operaCloud.Stays = [Stay("OC-1", "OC-G-1", "Alex Morgan", "415") with { Departure = departure.AddDays(2) }];
        ImportedStay second = Assert.Single((await ImportAsync()).Imported);

        Assert.Equal((first.StayContextId, StayImportOutcome.Updated), (second.StayContextId, second.Outcome));
        (StayContext stay, Guest guest) = await LoadAsync(second.StayContextId);
        Assert.Equal((room415.Id, departure.AddDays(2), "Alex Morgan"), (stay.AccommodationUnitId, stay.Departure, guest.Profile.DisplayName));
    }

    [Fact]
    public async Task Import_MatchesANativelyCreatedStayInTheSameUnit_InsteadOfDuplicatingIt()
    {
        (StayContextId nativeStay, GuestId nativeGuest) = await CreateNativeStayAsync(room412, arrival);
        await ConfigurePmsAsync("MockPms");

        ImportedStay imported = Assert.Single((await ImportAsync()).Imported);

        Assert.Equal((nativeStay, nativeGuest, StayImportOutcome.Matched), (imported.StayContextId, imported.GuestId, imported.Outcome));
        Assert.Equal(nativeStay.Value, await FindCanonicalAsync("MockPms", nameof(StayContext), "RES-0412"));
        Assert.Equal(nativeGuest.Value, await FindCanonicalAsync("MockPms", nameof(Guest), "G-77"));
    }

    [Fact]
    public async Task Import_OneGuestWithTwoRooms_CreatesOneGuest()
    {
        await ConfigurePmsAsync("OperaCloud");
        operaCloud.Stays = [Stay("OC-1", "OC-G-1", "Alex", "412"), Stay("OC-2", "OC-G-1", "Alex", "415")];

        StayImportReport report = await ImportAsync();

        Assert.Equal([StayImportOutcome.Created, StayImportOutcome.Created], report.Imported.Select(stay => stay.Outcome));
        Assert.Single(report.Imported.Select(stay => stay.GuestId).Distinct());
    }

    [Fact]
    public async Task Import_SkipsWhatItCannotApply_WithReasons()
    {
        await CreateNativeStayAsync(room415, arrival.AddDays(-3));
        await ConfigurePmsAsync("OperaCloud");
        operaCloud.Stays =
        [
            Stay("OC-1", "OC-G-1", "Alex", "412"),
            Stay("OC-2", "OC-G-2", "Sam", "412"),
            Stay("OC-3", "OC-G-3", "Kim", "999"),
            Stay("OC-4", "OC-G-4", "Lee", null),
            Stay("OC-5", "OC-G-5", "Max", "415"),
        ];

        StayImportReport report = await ImportAsync();

        Assert.Equal(["OC-1"], report.Imported.Select(stay => stay.PmsStayId.Value));
        Assert.Equal(
            [
                ("OC-2", "Another PMS stay in this import is in unit '412'."),
                ("OC-3", "Unit '999' is not a unit of the property."),
                ("OC-4", "The PMS has not assigned a unit."),
                ("OC-5", "Unit '415' is occupied by another in-house stay."),
            ],
            report.Skipped.Select(skipped => (skipped.PmsStayId.Value, skipped.Reason)));
    }

    [Fact]
    public async Task Import_WhenThePmsGuestIsLinkedToAnotherGuest_SkipsTheStayAndChangesNothing()
    {
        (StayContextId nativeStay, _) = await CreateNativeStayAsync(room412, arrival);
        (_, GuestId otherGuest) = await CreateNativeStayAsync(room415, arrival);
        await InTenantAsync(services => services.GetRequiredService<IExternalReferences>()
            .LinkAsync(Property, "MockPms", nameof(Guest), new ExternalId("G-77"), otherGuest.Value, Cancellation));
        await ConfigurePmsAsync("MockPms");

        StayImportReport report = await ImportAsync();

        Assert.Empty(report.Imported);
        Assert.Equal([("RES-0412", "The PMS guest is linked to a different guest than the stay's.")], report.Skipped.Select(skipped => (skipped.PmsStayId.Value, skipped.Reason)));
        Assert.Null(await FindCanonicalAsync("MockPms", nameof(StayContext), "RES-0412"));
        Assert.Equal(2, (await LoadAsync(nativeStay)).Stay.Version); // Created and checked in; untouched by the import.
    }

    [Fact]
    public async Task Import_WithoutAPmsConfigured_IsNotConfigured()
    {
        Result<StayImportReport> result = await InTenantAsync(services => services.GetRequiredService<IPmsStayImport>().ImportInHouseStaysAsync(Property, Cancellation));

        Assert.Equal(IntegrationRuntimeErrors.NotConfiguredCode, result.Error.Code);
    }

    [Fact]
    public async Task Journey18_SwappingMockPmsForAnotherProvider_KeepsCanonicalIdsAndEveryProvidersReferences()
    {
        await ConfigurePmsAsync("MockPms");
        ImportedStay fromMock = Assert.Single((await ImportAsync()).Imported);

        // Only configuration changes: the same import code, now reading the new provider's IDs for the same stay.
        await ConfigurePmsAsync("OperaCloud");
        operaCloud.Stays = [Stay("OPERA-77810", "OPERA-G-5", "Alex", "412")];
        StayImportReport fromOpera = await ImportAsync();

        ImportedStay matched = Assert.Single(fromOpera.Imported);
        Assert.Equal("OperaCloud", fromOpera.ProviderName);
        Assert.Equal((fromMock.StayContextId, fromMock.GuestId, StayImportOutcome.Matched), (matched.StayContextId, matched.GuestId, matched.Outcome));
        IReadOnlyList<ExternalReference> stayReferences = await InTenantAsync(services =>
            services.GetRequiredService<IExternalReferences>().ListForCanonicalAsync(Property, nameof(StayContext), fromMock.StayContextId.Value, Cancellation));
        Assert.Equal([("MockPms", "RES-0412"), ("OperaCloud", "OPERA-77810")], stayReferences.Select(reference => (reference.ProviderName, reference.ExternalId)).Order());
        Assert.Equal(fromMock.GuestId.Value, await FindCanonicalAsync("OperaCloud", nameof(Guest), "OPERA-G-5"));
        Assert.Equal(fromMock.GuestId.Value, await FindCanonicalAsync("MockPms", nameof(Guest), "G-77"));
    }

    private static PmsStay Stay(string id, string guestId, string guestName, string? unit, PmsStayStatus status = PmsStayStatus.InHouse) =>
        new(new ExternalId(id), new PmsGuest(new ExternalId(guestId), guestName, "en"), arrival, departure, unit, status);

    private async Task ConfigurePmsAsync(string provider)
    {
        Result<IntegrationConfigurationId> configured = await InTenantAsync(
            services => services.GetRequiredService<IIntegrationConfigurator>().ConfigureAsync(Property, ConnectorKind.Pms, provider, new Dictionary<string, string>(), Cancellation));

        Assert.True(configured.IsSuccess, configured.IsFailure ? configured.Error.Message : null);
    }

    private async Task<StayImportReport> ImportAsync()
    {
        Result<StayImportReport> report = await InTenantAsync(services => services.GetRequiredService<IPmsStayImport>().ImportInHouseStaysAsync(Property, Cancellation));

        Assert.True(report.IsSuccess, report.IsFailure ? report.Error.Message : null);

        return report.Value;
    }

    private async Task<(StayContextId Stay, GuestId Guest)> CreateNativeStayAsync(AccommodationUnit unit, DateOnly stayArrival)
    {
        (StayContextId, GuestId) ids = default;
        await InTenantAsync(async services =>
        {
            Guest guest = Guest.Register(new GuestId(Guid.CreateVersion7()), aurora.TenantId, new GuestProfile("Alex"), AuroraGrand.Now).Value;
            StayContext stay = StayContext.Create(new StayContextId(Guid.CreateVersion7()), guest, Property, stayArrival, departure, unit.Id, AuroraGrand.Now).Value;
            stay.CheckIn(null, AuroraGrand.Now);
            await services.GetRequiredService<IGuestRepository>().AddAsync(guest, Cancellation);
            await services.GetRequiredService<IStayContextRepository>().AddAsync(stay, Cancellation);
            ids = (stay.Id, guest.Id);

            return Result.Success();
        });

        return ids;
    }

    private Task<(StayContext Stay, Guest Guest)> LoadAsync(StayContextId stayId) =>
        InTenantAsync(async services =>
        {
            StayContext stay = (await services.GetRequiredService<IStayContextRepository>().GetByIdAsync(stayId, Cancellation))!;
            Guest guest = (await services.GetRequiredService<IGuestRepository>().GetByIdAsync(stay.GuestId, Cancellation))!;

            return (stay, guest);
        });

    private Task<Guid?> FindCanonicalAsync(string provider, string entityType, string externalId) =>
        InTenantAsync(services => services.GetRequiredService<IExternalReferences>().FindCanonicalIdAsync(Property, provider, entityType, new ExternalId(externalId), Cancellation));

    /// <summary>
    /// Runs <paramref name="action"/> in a new scope acting for Aurora's tenant and commits what it staged unless it
    /// returned a failed <see cref="Result"/>.
    /// </summary>
    private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);

        T result = await action(scope.ServiceProvider);

        if (result is not Result { IsFailure: true })
        {
            Result<CommitReceipt> committed = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation);
            Assert.True(committed.IsSuccess, committed.IsFailure ? committed.Error.Message : null);
        }

        return result;
    }
}
