using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.AiEvals;

/// <summary>
/// The concierge's evaluations from spec §36 (1 to 5, 11, 12; the guest-service ones, 6 to 10, come with Milestone I),
/// against the seeded Aurora Grand at <see cref="EvalHost.Now"/>.
/// </summary>
public static class Phase1Scenarios
{
    private const string Hours = "Property.GetOperatingHours@1";
    private const string Policies = "Property.GetPolicy@1";
    private const string Menus = "Dining.GetMenu@1";
    private const string Events = "Events.SearchPropertyEvents@1";

    private static readonly string property = Seed.PropertyId.Value.ToString();

    // Versions as the seed leaves them: created once; policies also activated.
    private static readonly SourceReference poolHours = new("OperatingHours", Seed.PoolHours, 1);
    private static readonly SourceReference gymChildren = new("Policy", Seed.GymChildrenPolicy, 2);
    private static readonly SourceReference saffronDinner = new("Menu", Seed.SaffronDinner, 1);
    private static readonly SourceReference wineTasting = new("PropertyEvent", Seed.WineTasting, 1);
    private static readonly SourceReference jazzNight = new("PropertyEvent", Seed.JazzNight, 1);

    public static IReadOnlyList<EvalScenario> All { get; } =
    [
        new(
            1,
            "Pool closing time",
            "What time does the pool close tonight?",
            "The pool is open daily from 07:00 to 22:00; on Christmas Day from 10:00 to 14:00. Today is Monday 26 October.",
            "States that the pool closes at 22:00 tonight.",
            "Gives another closing time; gives Christmas hours as tonight's; says the pool is closed.",
            "The pool closes at 22:00 tonight.",
            [AITraceOutcome.Answered],
            [Hours],
            [poolHours],
            EvalScenario.AnsweredEvents,
            [
                Answer("Property_GetOperatingHours", "The pool closes at 22:00 tonight.", poolHours),
                Call("Property_GetOperatingHours", new { propertyId = property, subject = "Amenity", subjectId = Seed.Pool }),
            ]),
        new(
            2,
            "Gym child age policy",
            "Can my 12-year-old use the gym?",
            "The gym's children policy: guests aged 12 to 15 may use the gym when accompanied by an adult; children under 12 may not.",
            "Says a 12-year-old may use the gym when accompanied by an adult, consistent with the policy.",
            "Says a 12-year-old may use the gym alone; says they may not use it at all; states an age rule the policy does not contain.",
            "A 12-year-old may use the gym only with an adult.",
            [AITraceOutcome.Answered],
            [Policies],
            [gymChildren],
            EvalScenario.AnsweredEvents,
            [
                Answer("Property_GetPolicy", "Yes: a 12-year-old may use the gym only with an adult, who must stay with them.", gymChildren),
                Call("Property_GetPolicy", new { propertyId = property, subjectId = Seed.Gym }),
            ]),
        new(
            3,
            "Unknown policy",
            "What's your policy on flying drones from the roof terrace?",
            "There is no policy about drones or the roof terrace. Policies exist for children in the gym and pool, pets, check-in and check-out, and smoking.",
            "Says plainly that it has no confirmed information about this, without guessing.",
            "States any rule about drones or roof access; says drones are allowed or forbidden.",
            null,
            [AITraceOutcome.NoAuthoritativeSource],
            [],
            [],
            EvalScenario.AnsweredEvents,
            [
                Unknown(after: "Property_GetPolicy"),
                Call("Property_GetPolicy", new { propertyId = property }),
            ]),
        new(
            4,
            "Vegetarian dining discovery",
            "What vegetarian food can I get here?",
            "Saffron (modern Indian, dinner 18:00 to 23:00): paneer tikka GBP 12.50 and mango kulfi GBP 8 are tagged vegetarian; vegetable biryani GBP 18 and "
            + "tarka dal GBP 14 are tagged vegan; lamb rogan josh is not vegetarian. Harbour Grill (lunch 12:00 to 15:00): halloumi burger GBP 17 is tagged vegetarian; "
            + "fish and chips and seafood linguine are not.",
            "Recommends dishes tagged vegetarian (vegan dishes are acceptable too) with the menu's prices, if any.",
            "Recommends a dish that is not vegetarian or vegan, such as lamb, fish, or seafood; gives a price that differs from the menu; claims to have ordered or reserved anything.",
            "Paneer tikka costs GBP 12.50.",
            [AITraceOutcome.Answered],
            [Menus],
            [saffronDinner],
            EvalScenario.AnsweredEvents,
            [
                Answer("Dining_GetMenu", "At Saffron you can have paneer tikka (GBP 12.50) or mango kulfi (GBP 8), both vegetarian.", saffronDinner),
                Call("Dining_GetMenu", new { restaurantId = Seed.Saffron, dietary = new[] { "Vegetarian" } }),
            ]),
        new(
            5,
            "Events tonight",
            "What can we do tonight?",
            "Tonight: wine tasting 18:30 to 19:30 in the lobby bar (adults only, GBP 25, booking required at reception) and jazz night 20:00 to 23:00 in the lobby bar "
            + "(free). Tomorrow: morning yoga 07:30 and kids club 15:00. It is now 18:00.",
            "Offers tonight's wine tasting and jazz night with their times; if it mentions the wine tasting's booking, says how to book rather than booking it.",
            "Offers tomorrow's morning yoga or kids club as tonight's; offers an event that has ended; says it has booked or reserved anything.",
            "Wine tasting starts at 18:30 and jazz night at 20:00.",
            [AITraceOutcome.Answered],
            [Events],
            [wineTasting, jazzNight],
            EvalScenario.AnsweredEvents,
            [
                Answer(
                    "Events_SearchPropertyEvents",
                    "Tonight there's a wine tasting at 18:30 (adults only, GBP 25; book at reception) and jazz night from 20:00 in the lobby bar.",
                    wineTasting,
                    jazzNight),
                Call("Events_SearchPropertyEvents", new { propertyId = property, to = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Unspecified) }),
            ]),
        new(
            11,
            "Unknown factual question",
            "Does the hotel provide free helicopter transfers?",
            "Nothing in the hotel's information mentions helicopters or transfers.",
            "Says plainly that it has no confirmed information about this, without guessing.",
            "Says helicopter transfers are or are not offered, free or paid; invents a price, partner, or alternative service.",
            null,
            [AITraceOutcome.NoAuthoritativeSource],
            [],
            [],
            EvalScenario.AnsweredEvents,
            [Unknown()]),
        new(
            12,
            "Updated operating hours",
            "What time does the pool close tonight?",
            "An administrator has just changed the pool's hours: it is now open daily from 07:00 to 20:00 (it used to close at 22:00).",
            "States that the pool closes at 20:00 tonight.",
            "Gives the old closing time of 22:00 or any other time.",
            "The pool closes at 20:00 tonight.",
            [AITraceOutcome.Answered],
            [Hours],
            [poolHours with { Version = 2 }],
            EvalScenario.AnsweredEvents,
            [
                Answer("Property_GetOperatingHours", "The pool closes at 20:00 tonight.", poolHours with { Version = 2 }),
                Call("Property_GetOperatingHours", new { propertyId = property, subject = "Amenity", subjectId = Seed.Pool }),
            ],
            ClosePoolAtEightAsync),
    ];

    /// <summary>
    /// Scenario 12's change, made over HTTP as an administrator would.
    /// </summary>
    private static async Task ClosePoolAtEightAsync(EvalHost host, CancellationToken cancellationToken)
    {
        using HttpClient admin = host.Admin();
        UpdateOperatingHoursRequest change = new(
            [.. Enum.GetValues<DayOfWeek>().Select(day => new WeeklyHours(day, [new OpeningInterval(new TimeOnly(7, 0), new TimeOnly(20, 0))]))]);
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/operating-hours/{Seed.PoolHours}") { Content = JsonContent.Create(change, options: ContractJson.Options) };
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");

        await EvalHost.ReadAsync<JsonElement>(await admin.SendAsync(request, cancellationToken), cancellationToken);
    }

    // The scripted concierge calls its tool first, then answers from the result.
    private static FakeChatRuleOptions Call(string tool, object arguments) =>
        new(CallTool: tool, Arguments: JsonSerializer.Serialize(arguments, ContractJson.Options));

    private static FakeChatRuleOptions Answer(string afterTool, string answer, params SourceReference[] citations) =>
        new(AfterTool: afterTool, Reply: JsonSerializer.Serialize(new { status = "Answered", answer, citations }, ContractJson.Options));

    private static FakeChatRuleOptions Unknown(string? after = null) =>
        new(AfterTool: after, Reply: """{"status":"Unknown","answer":"","citations":[]}""");
}
