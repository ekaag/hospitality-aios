using System.Text.Json;
using System.Threading.Channels;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.Workflows.Routing;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Live updates for the staff surface (J2): staff connected to <c>/hubs/realtime</c> with their token hear
/// <c>WorkChanged</c> for each change to their tenant's work, naming the work and the event but nothing of what the work
/// is; staff of another tenant hear nothing; and nobody connects without a staff token.
/// </summary>
public sealed class RealtimeHubTests : IAsyncLifetime
{
    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Staff_HearTheirTenantsWorkChange_AndOnlyThat()
    {
        Channel<JsonElement> heard = Channel.CreateUnbounded<JsonElement>();
        Channel<JsonElement> elsewhere = Channel.CreateUnbounded<JsonElement>();
        await using HubConnection jo = await ConnectAsync(StaffApi.StaffToken(Seed.TenantId, "Worker", Seed.JoOkafor.ToString()), heard);
        await using HubConnection other = await ConnectAsync(StaffApi.StaffToken(new TenantId(Guid.NewGuid()), "Manager"), elsewhere);

        Guid work = await CreateWorkAsync();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        List<(Guid Id, string Change)> changes = [];

        while (!changes.Contains((work, "HumanActionAssigned")))
        {
            Task<JsonElement> next = heard.Reader.ReadAsync(timeout.Token).AsTask();

            while (!next.IsCompleted)
            {
                api.Clock.Advance(TimeSpan.FromMilliseconds(50));
                await Task.WhenAny(next, Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token));
            }

            JsonElement message = await next;
            changes.Add((message.GetProperty("humanActionId").GetGuid(), message.GetProperty("change").GetString()!));
            Assert.Equal(["humanActionId", "change"], message.EnumerateObject().Select(property => property.Name));
        }

        Assert.Contains((work, "HumanActionRequested"), changes);
        Assert.False(elsewhere.Reader.TryRead(out _));
    }

    [Fact]
    public async Task WithoutAStaffToken_NobodyConnects()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using HubConnection anonymous = await ConnectAsync(null, Channel.CreateUnbounded<JsonElement>());
        });
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using HubConnection guest = await ConnectAsync(
                StaffApi.GuestToken(Seed.TenantId, new GuestStay(Seed.Alex, Seed.AlexStay, Seed.PropertyId)), Channel.CreateUnbounded<JsonElement>());
        });
    }

    private async Task<HubConnection> ConnectAsync(string? token, Channel<JsonElement> heard)
    {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Factory.Server.BaseAddress, "hubs/realtime"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
        connection.On<JsonElement>("WorkChanged", message => heard.Writer.TryWrite(message));
        await connection.StartAsync(Cancellation);

        return connection;
    }

    private async Task<Guid> CreateWorkAsync()
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(Actor.Agent("guestservice.extract"));
        Result<CapabilityResponse> created = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(
                "HumanAction.Create",
                JsonSerializer.SerializeToElement(
                    new CreateActionInput(
                        Seed.PropertyId,
                        ActionType.DeliverItems,
                        "Deliver 1 × Pillow",
                        new ActionLocation("Room 412", Seed.Room412),
                        "Guest asked for a pillow",
                        SlaMinutes: 30,
                        Items: [new ActionItemEntry("Pillow", 1, "pillow")],
                        SkillsRequired: ["housekeeping"]),
                    ContractJson.Options),
                IdempotencyKey: "work-1"),
            Cancellation);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

        return created.Value.Output.Deserialize<HumanActionOutput>(ContractJson.Options)!.Action.Source.Id;
    }
}
