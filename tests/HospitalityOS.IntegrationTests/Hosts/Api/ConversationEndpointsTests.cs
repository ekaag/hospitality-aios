using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// <c>/api/v1/conversations</c> with stay tokens (Journey 5): a guest starts a conversation linked to their stay and posts
/// messages, each raising <c>GuestMessageReceived</c> once however often it is sent; guests reach only their own stay's
/// conversations while the stay is active; and stay tokens and staff tokens each open only their own endpoints. Events are
/// not dispatched here, so the concierge does not answer.
/// </summary>
public sealed class ConversationEndpointsTests : IAsyncLifetime
{
    private const string Route = "/api/v1/conversations";

    private readonly TenantId tenant = new(Guid.CreateVersion7());
    private readonly StaffApi api = new();
    private GuestStay alex = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        alex = await GuestStay.SaveAsync(api.Factory.Services, tenant, property.Id, api.Clock.GetUtcNow(), Cancellation);
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Start_LinksTheConversationToTheTokensStay_AsTheGuest()
    {
        using HttpClient guest = api.Guest(tenant, alex);

        using HttpResponseMessage response = await guest.PostAsync(Route, null, Cancellation);

        ConversationResponse conversation = await StaffApi.CreatedAsync<ConversationResponse>(response, Cancellation);
        Assert.Equal($"{Route}/{conversation.Id}", response.Headers.Location?.ToString());
        Assert.Equal((alex.PropertyId, alex.StayContextId, ConversationStatus.Open), (conversation.PropertyId, conversation.StayContextId, conversation.Status));
        Assert.Equal(conversation, await StaffApi.OkAsync<ConversationResponse>(await guest.GetAsync($"{Route}/{conversation.Id}", Cancellation), Cancellation));
        EventMessage[] events = [.. (await api.EventsAsync(tenant, Cancellation)).Where(message => message.AggregateId == conversation.Id.Value)];
        Assert.Equal(["ConversationStarted", "ConversationUpdated"], events.Select(message => message.EventType));
        Assert.All(events, message => Assert.Equal(("Guest", alex.GuestId.Value.ToString()), Actor(message)));
    }

    [Fact]
    public async Task PostMessage_RaisesGuestMessageReceivedOnce_HoweverOftenTheSameClientMessageIdIsSent()
    {
        using HttpClient guest = api.Guest(tenant, alex);
        ConversationResponse conversation = await StartAsync(guest);
        object message = new { text = "  What time does the pool close tonight?  ", clientMessageId = "web-1" };

        using HttpResponseMessage first = await guest.PostAsJsonAsync($"{Route}/{conversation.Id}/messages", message, Cancellation);
        using HttpResponseMessage again = await guest.PostAsJsonAsync($"{Route}/{conversation.Id}/messages", message, Cancellation);

        GuestMessageResponse recorded = await StaffApi.CreatedAsync<GuestMessageResponse>(first, Cancellation);
        Assert.Equal(
            (conversation.Id, MessageSender.Guest, "What time does the pool close tonight?", "web-1"),
            (recorded.ConversationId, recorded.Sender, recorded.Text, recorded.ClientMessageId));
        Assert.Equal(recorded, await StaffApi.OkAsync<GuestMessageResponse>(again, Cancellation));
        Assert.Equal([recorded], await ListAsync(guest, conversation));
        EventMessage received = Assert.Single(await api.EventsAsync(tenant, Cancellation), candidate => candidate.EventType == "GuestMessageReceived");
        Assert.Equal(recorded.Id.Value, received.AggregateId);
        Assert.Equal(("Guest", alex.GuestId.Value.ToString()), Actor(received));
    }

    [Fact]
    public async Task PostMessage_WithoutText_IsAFieldError()
    {
        using HttpClient guest = api.Guest(tenant, alex);
        ConversationResponse conversation = await StartAsync(guest);

        using HttpResponseMessage response = await guest.PostAsJsonAsync($"{Route}/{conversation.Id}/messages", new { text = " " }, Cancellation);

        Assert.Equal(["message.text_required"], await StaffApi.FieldCodesAsync(response, Cancellation));
    }

    [Fact]
    public async Task AnotherStaysConversation_IsNotFound()
    {
        GuestStay other = await GuestStay.SaveAsync(api.Factory.Services, tenant, alex.PropertyId, api.Clock.GetUtcNow(), Cancellation);
        using HttpClient guest = api.Guest(tenant, alex);
        using HttpClient stranger = api.Guest(tenant, other);
        ConversationResponse conversation = await StartAsync(guest);

        using HttpResponseMessage read = await stranger.GetAsync($"{Route}/{conversation.Id}/messages", Cancellation);
        using HttpResponseMessage post = await stranger.PostAsJsonAsync($"{Route}/{conversation.Id}/messages", new { text = "Hello" }, Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, "conversation.not_found"), await StaffApi.ProblemAsync(read, Cancellation));
        Assert.Equal((HttpStatusCode.NotFound, "conversation.not_found"), await StaffApi.ProblemAsync(post, Cancellation));
    }

    [Fact]
    public async Task ACheckedOutStay_OrAStayTheTokenDoesNotMatch_IsRefused()
    {
        GuestStay departed = await GuestStay.SaveAsync(api.Factory.Services, tenant, alex.PropertyId, api.Clock.GetUtcNow(), Cancellation, StayStatus.CheckedOut);
        using HttpClient checkedOut = api.Guest(tenant, departed);
        using HttpClient wrongGuest = api.Guest(tenant, alex with { GuestId = departed.GuestId });
        using HttpClient wrongProperty = api.Guest(tenant, alex with { PropertyId = new(Guid.CreateVersion7()) });

        foreach (HttpClient guest in new[] { checkedOut, wrongGuest, wrongProperty })
        {
            using HttpResponseMessage response = await guest.PostAsync(Route, null, Cancellation);

            Assert.Equal((HttpStatusCode.Forbidden, "guest.stay_not_active"), await StaffApi.ProblemAsync(response, Cancellation));
        }
    }

    [Fact]
    public async Task StayTokensOpenOnlyConversations_AndStaffTokensEverythingElse()
    {
        using HttpClient guest = api.Guest(tenant, alex);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage guestOnStaff = await guest.GetAsync("/api/v1/properties", Cancellation);
        using HttpResponseMessage staffOnGuest = await admin.PostAsync(Route, null, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, guestOnStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staffOnGuest.StatusCode);
    }

    [Theory]
    [InlineData("stay_id")]
    [InlineData("property_id")]
    [InlineData("tenant_id")]
    [InlineData("sub")]
    [InlineData("staff role")]
    public async Task AnIncompleteOrMixedStayToken_IsRefused(string fault)
    {
        Dictionary<string, object> claims = new()
        {
            ["sub"] = alex.GuestId.Value.ToString(),
            ["tenant_id"] = tenant.Value.ToString(),
            ["property_id"] = alex.PropertyId.Value.ToString(),
            ["stay_id"] = alex.StayContextId.Value.ToString(),
            ["role"] = "Guest",
        };

        if (fault == "staff role")
        {
            claims["role"] = new[] { "Guest", "Admin" };
        }
        else
        {
            claims.Remove(fault);
        }

        using HttpClient guest = api.Client(StaffApi.Token(claims));

        using HttpResponseMessage start = await guest.PostAsync(Route, null, Cancellation);
        using HttpResponseMessage staff = await guest.GetAsync("/api/v1/properties", Cancellation);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (start.StatusCode, staff.StatusCode));
    }

    private static async Task<ConversationResponse> StartAsync(HttpClient guest) =>
        await StaffApi.CreatedAsync<ConversationResponse>(await guest.PostAsync(Route, null, Cancellation), Cancellation);

    private static async Task<IReadOnlyList<GuestMessageResponse>> ListAsync(HttpClient guest, ConversationResponse conversation) =>
        await StaffApi.OkAsync<IReadOnlyList<GuestMessageResponse>>(await guest.GetAsync($"{Route}/{conversation.Id}/messages", Cancellation), Cancellation);

    private static (string? Type, string? Id) Actor(EventMessage message)
    {
        using JsonDocument envelope = JsonDocument.Parse(message.Envelope);

        return (envelope.RootElement.GetProperty("actorType").GetString(), envelope.RootElement.GetProperty("actorId").GetString());
    }
}
