using System.Text.Json;
using System.Threading.Channels;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.IntegrationTests.Workflows;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
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
/// Live updates (J2, J3): staff connected to <c>/hubs/realtime</c> with their token hear <c>WorkChanged</c> for each change
/// to their tenant's work, naming the work and the event but nothing of what the work is, and staff of another tenant hear
/// nothing; a guest hears <c>StayChanged</c> for their own stay's messages and requests, naming what changed but not what
/// it says, and nothing of the staff's, while other stays and staff hear nothing of theirs; nobody connects without a token.
/// </summary>
public sealed class RealtimeHubTests : IAsyncLifetime
{
    private const string WorkChanged = "WorkChanged";
    private const string StayChanged = "StayChanged";

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true, configure: AuroraGrandScriptTests.Scripted);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Staff_HearTheirTenantsWorkChange_AndOnlyThat()
    {
        Channel<JsonElement> heard = Channel.CreateUnbounded<JsonElement>();
        Channel<JsonElement> elsewhere = Channel.CreateUnbounded<JsonElement>();
        await using HubConnection jo = await ConnectAsync(StaffApi.StaffToken(Seed.TenantId, "Worker", Seed.JoOkafor.ToString()), (WorkChanged, heard));
        await using HubConnection other = await ConnectAsync(StaffApi.StaffToken(new TenantId(Guid.NewGuid()), "Manager"), (WorkChanged, elsewhere));

        Guid work = await CreateWorkAsync();

        List<(Guid Id, string Change)> changes = [];
        await HearUntilAsync(heard, message =>
        {
            changes.Add((message.GetProperty("humanActionId").GetGuid(), message.GetProperty("change").GetString()!));
            Assert.Equal(["humanActionId", "change"], message.EnumerateObject().Select(property => property.Name));

            return changes.Contains((work, "HumanActionAssigned"));
        });

        Assert.Contains((work, "HumanActionRequested"), changes);
        Assert.False(elsewhere.Reader.TryRead(out _));
    }

    [Fact]
    public async Task AGuest_HearsTheirStaysChanges_AndNothingOfTheStaffs()
    {
        GuestStay alex = new(Seed.Alex, Seed.AlexStay, Seed.PropertyId);
        Channel<JsonElement> heard = Channel.CreateUnbounded<JsonElement>();
        Channel<JsonElement> work = Channel.CreateUnbounded<JsonElement>();
        Channel<JsonElement> otherStay = Channel.CreateUnbounded<JsonElement>();
        Channel<JsonElement> staff = Channel.CreateUnbounded<JsonElement>();
        await using HubConnection guest = await ConnectAsync(StaffApi.GuestToken(Seed.TenantId, alex), (StayChanged, heard), (WorkChanged, work));
        await using HubConnection stranger = await ConnectAsync(
            StaffApi.GuestToken(Seed.TenantId, new GuestStay(new GuestId(Guid.NewGuid()), new StayContextId(Guid.NewGuid()), Seed.PropertyId)),
            (StayChanged, otherStay));
        await using HubConnection manager = await ConnectAsync(StaffApi.StaffToken(Seed.TenantId, "Manager"), (StayChanged, staff));
        using HttpClient client = api.Guest(Seed.TenantId, alex);
        ConversationResponse conversation = await GuestChat.StartAsync(client, Cancellation);

        await GuestChat.PostAsync(client, conversation, "Could I get two towels please?", Cancellation);

        List<string> changes = [];
        await HearUntilAsync(heard, message =>
        {
            changes.Add(message.GetProperty("change").GetString()!);
            Assert.Equal(["change", "conversationId", "serviceRequestId"], message.EnumerateObject().Select(property => property.Name));

            return changes.Contains("GuestMessageSent") && changes.Contains("ServiceRequestCreated");
        });

        Assert.Contains("GuestMessageReceived", changes);
        Assert.False(work.Reader.TryRead(out _));
        Assert.False(otherStay.Reader.TryRead(out _));
        Assert.False(staff.Reader.TryRead(out _));
    }

    [Fact]
    public async Task WithoutAToken_NobodyConnects() =>
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using HubConnection anonymous = await ConnectAsync(null, (WorkChanged, Channel.CreateUnbounded<JsonElement>()));
        });

    private async Task<HubConnection> ConnectAsync(string? token, params (string Type, Channel<JsonElement> Heard)[] listens)
    {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Factory.Server.BaseAddress, "hubs/realtime"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();

        foreach ((string type, Channel<JsonElement> heard) in listens)
        {
            connection.On<JsonElement>(type, message => heard.Writer.TryWrite(message));
        }

        await connection.StartAsync(Cancellation);

        return connection;
    }

    /// <summary>
    /// Reads what <paramref name="heard"/> hears, advancing the host's clock so its dispatcher runs, until
    /// <paramref name="enough"/> says so, for at most ten seconds.
    /// </summary>
    private async Task HearUntilAsync(Channel<JsonElement> heard, Func<JsonElement, bool> enough)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        bool done = false;

        while (!done)
        {
            Task<JsonElement> next = heard.Reader.ReadAsync(timeout.Token).AsTask();

            while (!next.IsCompleted)
            {
                api.Clock.Advance(TimeSpan.FromMilliseconds(50));
                await Task.WhenAny(next, Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token));
            }

            done = enough(await next);
        }
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
