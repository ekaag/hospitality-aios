using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Events;
using HospitalityOS.Contracts.Api.Integrations;
using HospitalityOS.Contracts.Api.Menus;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Staff;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Seeding;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Seeding;

/// <summary>
/// The Mysore Grand, the reference tenant's second property, as a host seeded at startup sees it: staff of the tenant
/// see both hotels and can work in either, and each hotel's records, roster, and integrations are its own.
/// </summary>
public sealed class MysoreGrandSeedTests : IAsyncDisposable
{
    private readonly StaffApi api = new(seedAuroraGrand: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static TenantId Tenant => AuroraGrand.TenantId;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task TheTenantHasBothHotels_TheMysoreGrandInMysuru()
    {
        using HttpClient manager = api.Staff(Tenant, "Manager", "dana");

        IReadOnlyList<PropertyResponse>? properties = await manager.GetFromJsonAsync<IReadOnlyList<PropertyResponse>>("/api/v1/properties", ContractJson.Options, Cancellation);

        Assert.Equal(["Aurora Grand", "Mysore Grand"], properties!.Select(property => property.Name).Order());
        PropertyResponse mysore = Assert.Single(properties!, property => property.Id == MysoreGrand.PropertyId);
        Assert.Equal(("Asia/Kolkata", "Mysuru", "IN", MysoreGrand.OrganizationId), (mysore.TimeZone, mysore.Address.City, mysore.Address.CountryCode, mysore.OrganizationId));
    }

    [Fact]
    public async Task AmbarisDinnerMenu_IsPricedInRupees()
    {
        using HttpClient manager = api.Staff(Tenant, "Manager", "dana");

        IReadOnlyList<MenuResponse>? menus = await manager.GetFromJsonAsync<IReadOnlyList<MenuResponse>>(
            $"/api/v1/menus?restaurantId={MysoreGrand.Ambari}", ContractJson.Options, Cancellation);

        MenuResponse dinner = Assert.Single(menus!);
        Assert.Equal((MysoreGrand.AmbariDinner, MysoreGrand.PropertyId), (dinner.Id, dinner.PropertyId));
        Assert.Equal(
            [MysoreGrand.MysoreMasalaDosa, MysoreGrand.BisiBeleBath, MysoreGrand.ChickenGheeRoast, MysoreGrand.MysorePak, MysoreGrand.FilterCoffee],
            dinner.Items.Select(item => item.Id));
        Assert.All(dinner.Items, item => Assert.Equal("INR", item.Price?.CurrencyCode));
    }

    [Fact]
    public async Task EachHotelHasItsOwnRosterAndEvents()
    {
        using HttpClient manager = api.Staff(Tenant, "Manager", "dana");

        IReadOnlyList<StaffMemberResponse>? mysoreStaff = await manager.GetFromJsonAsync<IReadOnlyList<StaffMemberResponse>>(
            $"/api/v1/staff?propertyId={MysoreGrand.PropertyId}", ContractJson.Options, Cancellation);
        IReadOnlyList<StaffMemberResponse>? auroraStaff = await manager.GetFromJsonAsync<IReadOnlyList<StaffMemberResponse>>(
            $"/api/v1/staff?propertyId={AuroraGrand.PropertyId}", ContractJson.Options, Cancellation);
        IReadOnlyList<PropertyEventResponse>? events = await manager.GetFromJsonAsync<IReadOnlyList<PropertyEventResponse>>(
            $"/api/v1/events?propertyId={MysoreGrand.PropertyId}", ContractJson.Options, Cancellation);

        Assert.Equal(["Arjun Nair", "Kiran Gowda", "Lakshmi Iyer"], mysoreStaff!.Select(member => member.DisplayName).Order());
        Assert.DoesNotContain(auroraStaff!, member => member.PropertyId == MysoreGrand.PropertyId);
        Assert.Contains(events!, entry => (entry.Id, entry.Title) == (MysoreGrand.IlluminationWalk, "Palace illumination walk"));
        Assert.All(events!, entry => Assert.Equal(MysoreGrand.PropertyId, entry.PropertyId));
    }

    [Fact]
    public async Task TheMysoreGrandsIntegrationsAreConfigured()
    {
        using HttpClient admin = api.Staff(Tenant);

        IntegrationsResponse? integrations = await admin.GetFromJsonAsync<IntegrationsResponse>(
            $"/api/v1/integrations?propertyId={MysoreGrand.PropertyId}", ContractJson.Options, Cancellation);

        Assert.Equal(["MockMessaging", "MockPms", "NativeCms"], integrations!.Connectors.Select(connector => connector.Provider).OfType<string>().Order());
    }
}
