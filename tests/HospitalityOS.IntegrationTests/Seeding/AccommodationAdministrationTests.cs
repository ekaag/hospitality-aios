using HospitalityOS.Application.Accommodations;
using HospitalityOS.Application.Properties;
using HospitalityOS.Application.Spaces;
using HospitalityOS.Contracts.Api.Accommodations;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.Seeding;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Seeding;

/// <summary>
/// Rooms, which have no HTTP API in Phase 1, through their administration service in the seeded Aurora Grand: numbers
/// are unique within a property (the PMS names rooms by number), and a room is at a space of its type's property.
/// </summary>
public sealed class AccommodationAdministrationTests : IAsyncDisposable
{
    private readonly StaffApi api = new(seedAuroraGrand: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task CreateUnit_WithANumberThePropertyUses_IsANumberError()
    {
        await using AsyncServiceScope scope = AuroraScope();
        IAccommodationAdministration accommodations = scope.ServiceProvider.GetRequiredService<IAccommodationAdministration>();

        Result<AccommodationUnitResponse> created = await accommodations.CreateUnitAsync(
            new CreateAccommodationUnitRequest(AuroraGrand.DeluxeKing, AuroraGrand.Room415Space, " 412 "), Cancellation);

        Assert.Equal(["accommodation_unit.number_taken"], FieldCodes(created));
    }

    [Fact]
    public async Task CreateUnit_AtAnotherPropertysSpace_IsRefused_AndWithoutASpaceOrType_ReportsThem()
    {
        await using AsyncServiceScope scope = AuroraScope();
        IAccommodationAdministration accommodations = scope.ServiceProvider.GetRequiredService<IAccommodationAdministration>();
        PropertyResponse bay = (await scope.ServiceProvider.GetRequiredService<IPropertyAdministration>().CreateAsync(
            new CreatePropertyRequest(AuroraGrand.OrganizationId, "Aurora Bay", "Europe/London", new PropertyAddress("2 Quay", null, "Brighton", null, "BN1 1AA", "GB")), Cancellation)).Value;
        SpaceResponse bayRoom = (await scope.ServiceProvider.GetRequiredService<ISpaceAdministration>().CreateAsync(
            new CreateSpaceRequest(bay.Id, Name: "Room 101", Kind: SpaceKind.Room), Cancellation)).Value;

        Result<AccommodationUnitResponse> elsewhere = await accommodations.CreateUnitAsync(new CreateAccommodationUnitRequest(AuroraGrand.DeluxeKing, bayRoom.Id, "101"), Cancellation);
        Result<AccommodationUnitResponse> noSpace = await accommodations.CreateUnitAsync(new CreateAccommodationUnitRequest(AuroraGrand.DeluxeKing, UnitNumber: "416"), Cancellation);
        Result<AccommodationUnitResponse> noType = await accommodations.CreateUnitAsync(new CreateAccommodationUnitRequest(SpaceId: AuroraGrand.Room415Space, UnitNumber: "416"), Cancellation);

        Assert.Equal(["accommodation_unit.space_other_property"], FieldCodes(elsewhere));
        Assert.Equal(["accommodation_unit.space_required"], FieldCodes(noSpace));
        Assert.Equal(["accommodation_unit.type_required"], FieldCodes(noType));
    }

    [Fact]
    public async Task CreateType_WithoutAnOccupancy_ReportsIt_WithTheDomainsErrors()
    {
        await using AsyncServiceScope scope = AuroraScope();

        Result<AccommodationTypeResponse> created = await scope.ServiceProvider.GetRequiredService<IAccommodationAdministration>().CreateTypeAsync(
            new CreateAccommodationTypeRequest(AuroraGrand.PropertyId), Cancellation);

        Assert.Equal(["accommodation_type.name_required", "accommodation_type.max_occupancy_required"], FieldCodes(created));
    }

    private static IReadOnlyList<string> FieldCodes<T>(Result<T> result) =>
        [.. Assert.IsType<ValidationError>(result.Error).Fields.Select(field => field.Code)];

    private AsyncServiceScope AuroraScope()
    {
        AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(AuroraGrand.TenantId);

        return scope;
    }
}
