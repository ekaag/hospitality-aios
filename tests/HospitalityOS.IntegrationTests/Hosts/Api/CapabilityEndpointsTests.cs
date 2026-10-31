using System.ComponentModel;
using System.Net;
using System.Text.Json;
using HospitalityOS.Capabilities;
using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

public sealed class CapabilityEndpointsTests : IAsyncLifetime
{
    private const string Route = "/api/v1/capabilities";

    private readonly WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services
            .AddCapability<GetHoursCapability>()
            .AddCapability<NotifyGuestCapability>()
            .AddCapability<NotifyGuestV2Capability>()));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task List_ReturnsEveryVersionWithEverySpecFieldInTheWireFormat()
    {
        using HttpClient client = factory.CreateClient();

        using JsonDocument body = JsonDocument.Parse(await client.GetStringAsync(Route, TestContext.Current.CancellationToken));

        JsonElement[] capabilities = [.. body.RootElement.EnumerateArray().Where(capability => capability.GetProperty("name").GetString()!.StartsWith("Test.", StringComparison.Ordinal))];
        Assert.Equal(
            ["Test.GetHours v1", "Test.NotifyGuest v1", "Test.NotifyGuest v2"],
            capabilities.Select(capability => $"{capability.GetProperty("name").GetString()} v{capability.GetProperty("version").GetInt32()}"));
        JsonElement getHours = capabilities[0];
        Assert.Equal(
            ["name", "version", "description", "inputSchema", "outputSchema", "authorization", "confirmationPolicy", "sideEffects", "idempotency", "auditClassification"],
            getHours.EnumerateObject().Select(field => field.Name));
        Assert.Equal(
            ("Guest", "NotRequired", "None", "Inherent", "Routine"),
            (getHours.GetProperty("authorization").GetProperty("allowedCallers")[1].GetString(),
                getHours.GetProperty("confirmationPolicy").GetString(),
                getHours.GetProperty("sideEffects").GetString(),
                getHours.GetProperty("idempotency").GetString(),
                getHours.GetProperty("auditClassification").GetString()));
        Assert.Equal(
            ("object", "uuid", "The property whose hours are wanted."),
            (getHours.GetProperty("inputSchema").GetProperty("type").GetString(),
                getHours.GetProperty("inputSchema").GetProperty("properties").GetProperty("propertyId").GetProperty("format").GetString(),
                getHours.GetProperty("inputSchema").GetProperty("properties").GetProperty("propertyId").GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Get_ByName_ReturnsTheLatestVersion()
    {
        using HttpClient client = factory.CreateClient();

        CapabilityDescription? description = await GetAsync(client, $"{Route}/Test.NotifyGuest");

        Assert.Equal(
            ("Test.NotifyGuest", 2, SideEffect.External, IdempotencyPolicy.KeyRequired, ConfirmationPolicy.Required, AuditClassification.Sensitive),
            (description?.Name, description?.Version, description?.SideEffects, description?.Idempotency, description?.ConfirmationPolicy, description?.AuditClassification));
    }

    [Fact]
    public async Task Get_ByNameAndVersion_ReturnsThatVersion()
    {
        using HttpClient client = factory.CreateClient();

        CapabilityDescription? description = await GetAsync(client, $"{Route}/Test.NotifyGuest?version=1");

        Assert.Equal(1, description?.Version);
    }

    [Theory]
    [InlineData("Test.Unknown")]
    [InlineData("test.notifyguest")]
    [InlineData("Test.NotifyGuest?version=3")]
    public async Task Get_Unknown_IsNotFound(string path)
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync($"{Route}/{path}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_InTheHostAsShipped_PublishesTheSpecCapabilities()
    {
        await using WebApplicationFactory<Program> shipped = new();
        using HttpClient client = shipped.CreateClient();

        using JsonDocument body = JsonDocument.Parse(await client.GetStringAsync(Route, TestContext.Current.CancellationToken));

        JsonElement[] capabilities = [.. body.RootElement.EnumerateArray()];
        Assert.Equal(
            [
                "Dining.GetMenu", "Dining.SearchRestaurants", "Events.SearchPropertyEvents", "GuestService.CreateRequest", "GuestService.GetRequestStatus",
                "Property.GetOperatingHours", "Property.GetPolicy", "Property.GetProperty", "Property.SearchContent",
            ],
            capabilities.Select(capability => capability.GetProperty("name").GetString()));
        JsonElement hoursInput = capabilities[5].GetProperty("inputSchema");
        Assert.Equal(
            "The amenity, restaurant, space, or service ID; required unless the subject is the property.",
            hoursInput.GetProperty("properties").GetProperty("subjectId").GetProperty("description").GetString());
        Assert.Equal(["propertyId"], hoursInput.GetProperty("required").EnumerateArray().Select(field => field.GetString()));
    }

    private static async Task<CapabilityDescription?> GetAsync(HttpClient client, string path)
    {
        string json = await client.GetStringAsync(path, TestContext.Current.CancellationToken);

        return JsonSerializer.Deserialize<CapabilityDescription>(json, ContractJson.Options);
    }

    private sealed record HoursInput([property: Description("The property whose hours are wanted.")] PropertyId PropertyId);

    private sealed record HoursOutput(string Summary);

    private sealed record NotifyInput(Guid StayId, string Text);

    private sealed record NotifyOutput(Guid MessageId);

    private sealed class GetHoursCapability : ICapability<HoursInput, HoursOutput>
    {
        public static CapabilityDescriptor Descriptor { get; } = new()
        {
            Name = "Test.GetHours",
            Version = 1,
            Description = "Returns today's opening hours.",
            Authorization = CapabilityAuthorization.Allow(CapabilityCaller.Agent, CapabilityCaller.Guest),
            ConfirmationPolicy = ConfirmationPolicy.NotRequired,
            SideEffects = SideEffect.None,
            Idempotency = IdempotencyPolicy.Inherent,
            AuditClassification = AuditClassification.Routine,
        };

        public Task<Result<HoursOutput>> ExecuteAsync(HoursInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Declaration only.");
    }

    private sealed class NotifyGuestCapability : ICapability<NotifyInput, NotifyOutput>
    {
        public static CapabilityDescriptor Descriptor { get; } = new()
        {
            Name = "Test.NotifyGuest",
            Version = 1,
            Description = "Sends the guest a message.",
            Authorization = CapabilityAuthorization.Allow(CapabilityCaller.System),
            ConfirmationPolicy = ConfirmationPolicy.Required,
            SideEffects = SideEffect.External,
            Idempotency = IdempotencyPolicy.KeyRequired,
            AuditClassification = AuditClassification.Sensitive,
        };

        public Task<Result<NotifyOutput>> ExecuteAsync(NotifyInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Declaration only.");
    }

    private sealed class NotifyGuestV2Capability : ICapability<NotifyInput, NotifyOutput>
    {
        public static CapabilityDescriptor Descriptor { get; } = NotifyGuestCapability.Descriptor with { Version = 2 };

        public Task<Result<NotifyOutput>> ExecuteAsync(NotifyInput input, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Declaration only.");
    }
}
