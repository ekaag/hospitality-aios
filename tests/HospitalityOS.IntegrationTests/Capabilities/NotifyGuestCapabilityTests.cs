using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities.Messaging;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Integrations.Mock.Messaging;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.IntegrationTests.Workflows;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Capabilities;

/// <summary>
/// <c>Messaging.NotifyGuest</c> through the executor in the real host: a message to Alex goes over the conversation's
/// channel through the Aurora Grand's messaging connector (MockMessaging) and is sent once per key (Journey 19); a
/// recipient the provider refuses, or a property with no messaging configured, is recorded as failed; and workers may not
/// message guests.
/// </summary>
public sealed class NotifyGuestCapabilityTests : IAsyncLifetime
{
    private static readonly Actor agent = Actor.Agent("guestservice.notify_completion");
    private static readonly GuestStay alex = new(Seed.Alex, Seed.AlexStay, Seed.PropertyId);
    private static readonly Guid towels = Guid.Parse("01890a5d-ac96-774b-bcce-00000000b001");

    private readonly StaffApi api = new(seedAuroraGrand: true);
    private ConversationResponse conversation = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        using HttpClient guest = api.Guest(Seed.TenantId, alex);
        conversation = await GuestChat.StartAsync(guest, Cancellation);
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AMessage_IsSentThroughThePropertysConnector_OncePerKey()
    {
        NotifyGuestOutput first = await NotifyAsync(Seed.TenantId, conversation.Id, "towels-done");
        NotifyGuestOutput again = await NotifyAsync(Seed.TenantId, conversation.Id, "towels-done");

        Assert.Equal((NotificationStatus.Sent, "MockMessaging", "GuestNotification"), (first.Status, first.ProviderName, first.Source.Type));
        Assert.Equal(first, again);
        DeliveredMessage delivered = Assert.Single(Gateway.DeliveredFor(Seed.PropertyId));
        Assert.Equal(("towels-done", conversation.Id.ToString(), "Your towels are here."), (delivered.Message.IdempotencyKey, delivered.Message.Recipient.Address, delivered.Message.Body));
        IReadOnlyList<string> events = [.. (await api.EventsAsync(Seed.TenantId, Cancellation)).Where(message => message.AggregateId == first.Source.Id).Select(message => message.EventType)];
        Assert.Equal(["GuestNotificationRequested", "GuestNotificationSent"], events);
    }

    [Fact]
    public async Task ARecipientTheProviderRefuses_IsRecordedAsFailed()
    {
        api.Factory.Services.GetRequiredService<IOptions<MockMessagingOptions>>().Value.UnreachableAddresses.Add(conversation.Id.ToString());

        NotifyGuestOutput refused = await NotifyAsync(Seed.TenantId, conversation.Id, "towels-done");

        Assert.Equal((NotificationStatus.Failed, "integration.rejected"), (refused.Status, refused.FailureReason));
        Assert.Empty(Gateway.DeliveredFor(Seed.PropertyId));
        Assert.Contains(await api.EventsAsync(Seed.TenantId, Cancellation), message => message.EventType == "GuestNotificationFailed" && message.AggregateId == refused.Source.Id);
    }

    [Fact]
    public async Task APropertyWithNoMessagingConfigured_IsRecordedAsFailed()
    {
        AuroraGrand unconfigured = new(new TenantId(Guid.CreateVersion7()));
        await unconfigured.SaveAsync(api.Factory.Services, Cancellation);
        GuestStay stay = await GuestStay.SaveAsync(api.Factory.Services, unconfigured.TenantId, unconfigured.PropertyId, api.Clock.GetUtcNow(), Cancellation);
        using HttpClient guest = api.Guest(unconfigured.TenantId, stay);
        ConversationResponse elsewhere = await GuestChat.StartAsync(guest, Cancellation);

        NotifyGuestOutput failed = await NotifyAsync(unconfigured.TenantId, elsewhere.Id, "done");

        Assert.Equal((NotificationStatus.Failed, "integration.not_configured"), (failed.Status, failed.FailureReason));
    }

    [Fact]
    public async Task WorkersMayNotMessageGuests_AndAnUnknownConversationIsInvalid()
    {
        Error worker = (await RunAsync(Actor.Staff(Seed.JoOkafor.ToString(), StaffRole.Worker), Seed.TenantId, conversation.Id, "k1")).Error;
        Error unknown = (await RunAsync(agent, Seed.TenantId, new ConversationId(Guid.CreateVersion7()), "k2")).Error;

        Assert.Equal("capability.forbidden", worker.Code);
        Assert.Equal("notification.conversation_not_found", Assert.Single(Assert.IsType<ValidationError>(unknown).Fields).Code);
        Assert.Empty(Gateway.DeliveredFor(Seed.PropertyId));
    }

    private MockMessagingGateway Gateway => api.Factory.Services.GetRequiredService<MockMessagingGateway>();

    private async Task<NotifyGuestOutput> NotifyAsync(TenantId tenant, ConversationId to, string key)
    {
        Result<CapabilityResponse> result = await RunAsync(agent, tenant, to, key);
        Assert.True(result.IsSuccess, result.IsFailure ? $"{result.Error.Code}: {result.Error.Message}" : null);

        return result.Value.Output.Deserialize<NotifyGuestOutput>(ContractJson.Options)!;
    }

    private async Task<Result<CapabilityResponse>> RunAsync(Actor actor, TenantId tenant, ConversationId to, string key)
    {
        await using AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(actor);

        return await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest(
                "Messaging.NotifyGuest",
                JsonSerializer.SerializeToElement(new NotifyGuestInput(to, "Your towels are here.", key, "ServiceRequest", towels), ContractJson.Options),
                IdempotencyKey: key),
            Cancellation);
    }
}
