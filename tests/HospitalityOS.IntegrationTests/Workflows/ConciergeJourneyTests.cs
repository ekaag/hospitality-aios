using System.Text.Json;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.Contracts.Capabilities.Dining;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.IntegrationTests.Capabilities.Reading;
using HospitalityOS.IntegrationTests.Hosts.Api;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.Workflows.Concierge;
using Xunit;
using static HospitalityOS.IntegrationTests.Workflows.GuestChat;

namespace HospitalityOS.IntegrationTests.Workflows;

/// <summary>
/// The concierge's guest journeys end to end over HTTP against the Aurora Grand on Monday evening, with the Fake model
/// scripted to call the tools a concierge would and to reply as it would: a policy question answered by the gym's policy
/// (Journey 6), a policy that does not cover the case answered with explicit uncertainty, and no invented rule where no
/// policy exists; vegetarian dining from dietary tags with menu prices and only available dishes (Journey 7); tonight's
/// events in property-local time without past or cancelled ones (Journey 8); and an unknown question answered honestly
/// and kept for content improvement (Journey 17). Whether a real model replies that way is for the AI evals (H3); these
/// show the tools give it the right authoritative data and that its replies are grounded, traced, and recorded.
/// </summary>
public sealed class ConciergeJourneyTests : IAsyncLifetime
{
    private static readonly string[] vegetarian = ["Vegetarian"];

    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly StaffApi api;
    private GuestStay alex = null!;

    public ConciergeJourneyTests()
    {
        api = Host(Script(aurora), now: AuroraGrand.Now);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await aurora.SaveAsync(api.Factory.Services, Cancellation);
        await aurora.IndexContentAsync(api.Factory.Services, Cancellation);
        alex = await GuestStay.SaveAsync(api.Factory.Services, aurora.TenantId, aurora.PropertyId, api.Clock.GetUtcNow(), Cancellation);
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task PolicyQuestion_IsAnsweredByTheGymsPolicy()
    {
        (GuestMessageResponse question, GuestMessageResponse answer, AITraceResponse trace) = await AskAsync("Can my 12-year-old use the gym?");

        Assert.Equal("Yes, with an adult: guests under 16 must be accompanied by an adult in the gym.", answer.Text);
        GetPolicyOutput read = ToolOutput<GetPolicyOutput>(trace, "Property.GetPolicy@1");
        Assert.Equal(Source(aurora.GymChildren), read.Policies[0].Source);
        Assert.Equal("Guests under 16 must be accompanied by an adult.", read.Policies[0].Content);
        Assert.Equal(AITraceOutcome.Answered, trace.Outcome);
        Assert.Equal([Source(aurora.GymChildren)], trace.Body.Response!.Citations);
        Assert.Equal("Answered", Payload(await AnsweredAsync(api, aurora.TenantId, question, Cancellation)).Outcome);
    }

    [Fact]
    public async Task APolicyThatDoesNotCoverTheCase_IsAnsweredWithExplicitUncertainty_CitingIt()
    {
        (GuestMessageResponse question, GuestMessageResponse answer, AITraceResponse trace) = await AskAsync("Can I bring my cat?");

        Assert.Equal("Our pet policy welcomes dogs under 15 kg but does not mention cats, so I can't confirm; the front desk can check.", answer.Text);
        Assert.Equal(Source(aurora.Pets), Assert.Single(ToolOutput<GetPolicyOutput>(trace, "Property.GetPolicy@1").Policies).Source);
        Assert.Equal(AITraceOutcome.Uncertain, trace.Outcome);
        Assert.Equal([Source(aurora.Pets)], trace.Body.Response!.Citations);
        Assert.Equal("Uncertain", Payload(await AnsweredAsync(api, aurora.TenantId, question, Cancellation)).Outcome);
    }

    [Fact]
    public async Task WithoutAPolicyInEffect_NoRuleIsInvented()
    {
        (_, GuestMessageResponse answer, AITraceResponse trace) = await AskAsync("Can I smoke in my room?");

        // The smoking policy is still a draft, so it is not authoritative.
        Assert.Empty(ToolOutput<GetPolicyOutput>(trace, "Property.GetPolicy@1").Policies);
        Assert.Equal((Concierge.NoSourceAnswer, AITraceOutcome.NoAuthoritativeSource), (answer.Text, trace.Outcome));
    }

    [Fact]
    public async Task VegetarianDining_ComesFromDietaryTags_WithMenuPrices_AndOnlyAvailableDishes()
    {
        (_, GuestMessageResponse answer, AITraceResponse trace) = await AskAsync("What vegetarian food can I get here?");

        Assert.Equal("Saffron serves paneer tikka (£14.50), a vegetarian starter.", answer.Text);
        SearchRestaurantsOutput restaurants = ToolOutput<SearchRestaurantsOutput>(trace, "Dining.SearchRestaurants@1");
        Assert.Equal([Source(aurora.Saffron)], restaurants.Restaurants.Select(restaurant => restaurant.Source));
        MenuItemEntry dish = Assert.Single(Assert.Single(ToolOutput<GetMenuOutput>(trace, "Dining.GetMenu@1").Menus).Items);
        Assert.Equal(("Paneer tikka", 14.50m, "GBP", true), (dish.Name, dish.Price?.Amount, dish.Price?.CurrencyCode, dish.IsAvailable));
        Assert.Equal(AITraceOutcome.Answered, trace.Outcome);
        Assert.Equal([Source(aurora.SaffronDinner)], trace.Body.Response!.Citations);
    }

    [Fact]
    public async Task TonightsEvents_AreInPropertyLocalTime_WithoutPastOrCancelledOnes()
    {
        (_, GuestMessageResponse answer, AITraceResponse trace) = await AskAsync("What can we do tonight?");

        Assert.Equal("The kids club runs until 19:00, and jazz night is from 20:00 to 22:00.", answer.Text);
        SearchPropertyEventsOutput events = ToolOutput<SearchPropertyEventsOutput>(trace, "Events.SearchPropertyEvents@1");
        Assert.Equal(
            [("Kids club", AuroraGrand.LocalNow.Date.AddHours(17)), ("Jazz night", AuroraGrand.LocalNow.Date.AddHours(20))],
            events.Events.Select(entry => (entry.Title, entry.LocalStart)));
        Assert.Equal(AITraceOutcome.Answered, trace.Outcome);
    }

    [Fact]
    public async Task ABookableEvent_SaysBookingIsRequired_AndTheConciergeCanOnlyRead()
    {
        (_, GuestMessageResponse answer, AITraceResponse trace) = await AskAsync("Can you book us into the cooking class tomorrow?");

        Assert.Equal("I can't make bookings, but the cooking class tomorrow at 15:00 (£45) needs one; the front desk can book it for you.", answer.Text);
        EventEntry cooking = Assert.Single(ToolOutput<SearchPropertyEventsOutput>(trace, "Events.SearchPropertyEvents@1").Events);
        Assert.Equal((Source(aurora.CookingClass), true), (cooking.Source, cooking.BookingRequired));

        // Nothing the concierge can call changes anything, so it cannot make the booking (or an order) itself.
        using HttpClient manager = api.Staff(aurora.TenantId, "Manager");

        foreach (string tool in Concierge.Tools)
        {
            string[] nameAndVersion = tool.Split('@');
            CapabilityDescription description = await StaffApi.OkAsync<CapabilityDescription>(
                await manager.GetAsync($"/api/v1/capabilities/{nameAndVersion[0]}?version={nameAndVersion[1]}", Cancellation), Cancellation);
            Assert.Equal(SideEffect.None, description.SideEffects);
        }
    }

    [Fact]
    public async Task AnUnknownQuestion_IsAnsweredHonestly_AndKeptForContentImprovement()
    {
        (GuestMessageResponse question, GuestMessageResponse answer, AITraceResponse trace) = await AskAsync("Does the hotel provide free helicopter transfers?");

        Assert.Equal(Concierge.NoSourceAnswer, answer.Text);
        Assert.Equal((AITraceOutcome.NoAuthoritativeSource, question.Text), (trace.Outcome, trace.Body.TriggerText));

        // Content editors find the questions nothing answered by listing the property's traces with that outcome.
        using HttpClient manager = api.Staff(aurora.TenantId, "Manager");
        AITracePage gaps = await StaffApi.OkAsync<AITracePage>(
            await manager.GetAsync($"/api/v1/ai/traces?propertyId={aurora.PropertyId}&outcome=NoAuthoritativeSource", Cancellation), Cancellation);
        AITraceSummary gap = Assert.Single(gaps.Traces);
        Assert.Equal((trace.Id, "GuestMessage", question.Id.Value), (gap.Id, gap.TriggerType, gap.TriggerId));
    }

    private static FakeScriptOptions Script(AuroraGrand aurora)
    {
        string property = aurora.PropertyId.Value.ToString();
        FakeScriptOptions concierge = new();

        // Most specific first: the first rule that matches the conversation decides the turn.
        concierge.Chat.Add(Reply("gym", "Property_GetPolicy", "Answered", "Yes, with an adult: guests under 16 must be accompanied by an adult in the gym.", Source(aurora.GymChildren)));
        concierge.Chat.Add(Reply("cat", "Property_GetPolicy", "Uncertain", "Our pet policy welcomes dogs under 15 kg but does not mention cats, so I can't confirm; the front desk can check.", Source(aurora.Pets)));
        concierge.Chat.Add(Reply("smoke", "Property_GetPolicy", "Unknown", "Smoking is allowed on balconies."));
        concierge.Chat.Add(Call("gym", "Property_GetPolicy", new { propertyId = property, subjectId = aurora.Gym.Id.Value }));
        concierge.Chat.Add(Call("cat", "Property_GetPolicy", new { propertyId = property, type = "Pets" }));
        concierge.Chat.Add(Call("smoke", "Property_GetPolicy", new { propertyId = property, type = "Smoking" }));

        concierge.Chat.Add(Reply("vegetarian", "Dining_GetMenu", "Answered", "Saffron serves paneer tikka (£14.50), a vegetarian starter.", Source(aurora.SaffronDinner)));
        concierge.Chat.Add(Call("vegetarian", "Dining_GetMenu", new { restaurantId = aurora.Saffron.Id.Value, dietary = vegetarian }, after: "Dining_SearchRestaurants"));
        concierge.Chat.Add(Call("vegetarian", "Dining_SearchRestaurants", new { propertyId = property, dietary = vegetarian }));

        DateTime midnight = AuroraGrand.LocalNow.Date.AddDays(1);
        concierge.Chat.Add(Reply("cooking", "Events_SearchPropertyEvents", "Answered", "I can't make bookings, but the cooking class tomorrow at 15:00 (£45) needs one; the front desk can book it for you.", Source(aurora.CookingClass)));
        concierge.Chat.Add(Reply("tonight", "Events_SearchPropertyEvents", "Answered", "The kids club runs until 19:00, and jazz night is from 20:00 to 22:00.", Source(aurora.KidsClub), Source(aurora.JazzNight)));
        concierge.Chat.Add(Call("cooking", "Events_SearchPropertyEvents", new { propertyId = property, from = midnight, to = midnight.AddDays(1), category = "FoodAndDrink", audience = "AdultsOnly" }));
        concierge.Chat.Add(Call("tonight", "Events_SearchPropertyEvents", new { propertyId = property, to = midnight }));

        concierge.Chat.Add(new FakeChatRuleOptions(WhenUserSays: "helicopter", Reply: """{"status":"Unknown","answer":"","citations":[]}"""));

        return concierge;

        static FakeChatRuleOptions Call(string says, string tool, object arguments, string? after = null) =>
            new(WhenUserSays: says, AfterTool: after, CallTool: tool, Arguments: JsonSerializer.Serialize(arguments, ContractJson.Options));

        static FakeChatRuleOptions Reply(string says, string afterTool, string status, string answer, params SourceReference[] citations) =>
            new(WhenUserSays: says, AfterTool: afterTool, Reply: JsonSerializer.Serialize(new { status, answer, citations }, ContractJson.Options));
    }

    private async Task<(GuestMessageResponse Question, GuestMessageResponse Answer, AITraceResponse Trace)> AskAsync(string text)
    {
        using HttpClient guest = api.Guest(aurora.TenantId, alex);
        (GuestMessageResponse question, GuestMessageResponse answer) = await GuestChat.AskAsync(api, guest, text, Cancellation);
        AITraceResponse trace = await TraceAsync(api, aurora.TenantId, (await ReceivedAsync(question)).CorrelationId, Cancellation);

        return (question, answer, trace);
    }

    private async Task<EventMessage> ReceivedAsync(GuestMessageResponse question) =>
        Assert.Single(await api.EventsAsync(aurora.TenantId, Cancellation), message => message.EventType == "GuestMessageReceived" && message.AggregateId == question.Id.Value);

    private static T ToolOutput<T>(AITraceResponse trace, string capability)
    {
        CapabilityCall call = Assert.Single(trace.Body.CapabilityCalls, call => call.Purpose == CapabilityCallPurpose.Tool && call.Capability == capability);

        return Assert.IsType<T>(call.Output!.Value.Deserialize<T>(ContractJson.Options));
    }
}
