using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Contracts.Api.Conversations;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Workflows.GuestService;
using HospitalityOS.Workflows.Triage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.AiEvals;

/// <summary>
/// The evaluations from spec §36 against the seeded Aurora Grand at <see cref="EvalHost.Now"/>: the concierge's questions
/// (1 to 5, 9, 11, 12) and the Guest Service Controller's requests (6 to 8, 10).
/// </summary>
public static class Phase1Scenarios
{
    private const string Hours = "Property.GetOperatingHours@1";
    private const string Policies = "Property.GetPolicy@1";
    private const string Menus = "Dining.GetMenu@1";
    private const string Events = "Events.SearchPropertyEvents@1";
    private const string CreateRequest = "GuestService.CreateRequest@1";
    private const string RequestStatus = "GuestService.GetRequestStatus@1";

    private const string TwoTowels = "Can you send two towels to my room?";

    private const string Amenities =
        "Alex is in house in Room 412. Housekeeping amenities delivers bath towels (at most 4 per request), hand towels, pillows "
        + "(at most 2), and toiletries kits to guests' rooms; staff are asked to do it, and no approval or payment is needed.";

    private const string NoPromise =
        "says or implies when it will be done, such as \"in 10 minutes\" or \"shortly\"; says it has already been done; says a manager "
        + "must approve it or that the guest must pay";

    private static readonly string property = Seed.PropertyId.Value.ToString();

    // Versions as the seed leaves them: created once; policies also activated.
    private static readonly SourceReference poolHours = new("OperatingHours", Seed.PoolHours, 1);
    private static readonly SourceReference gymChildren = new("Policy", Seed.GymChildrenPolicy, 2);
    private static readonly SourceReference saffronDinner = new("Menu", Seed.SaffronDinner, 1);
    private static readonly SourceReference wineTasting = new("PropertyEvent", Seed.WineTasting, 1);
    private static readonly SourceReference jazzNight = new("PropertyEvent", Seed.JazzNight, 1);

    // The Fake triage and request reading, as a model would classify and read each message.
    private static readonly FakeClassificationRuleOptions[] triaged =
    [
        new("where are my", GuestTriage.RequestStatusLabel),
        new("towel", GuestTriage.ServiceRequestLabel),
        new("pillow", GuestTriage.ServiceRequestLabel),
        new("AC", GuestTriage.ServiceRequestLabel),
    ];

    private static readonly FakeExtractionRuleOptions[] read =
    [
        Reading("two towels", """{"understood":true,"serviceCode":"housekeeping.amenities","itemCode":"bath_towel","quantity":2,"description":null,"more":false}"""),
        Reading("pillow", """{"understood":true,"serviceCode":"housekeeping.amenities","itemCode":"pillow","quantity":null,"description":null,"more":false}"""),
        Reading("AC", """{"understood":true,"serviceCode":"maintenance.repair","itemCode":null,"quantity":null,"description":"The air conditioning is not working","more":false}"""),
    ];

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
            6,
            "Request two towels",
            TwoTowels,
            Amenities,
            "Confirms that 2 towels will be brought to Room 412.",
            $"Names a different quantity, item, or room; {NoPromise}.",
            "2 towels will be brought to Room 412.",
            [AITraceOutcome.Answered],
            [RequestStatus, CreateRequest],
            [],
            EvalScenario.RequestedEvents,
            [],
            Agent: GuestServiceController.Task,
            Triaged: triaged,
            Read: read,
            SettledBy: "HumanActionAssigned",
            Verify: Requested("housekeeping.amenities", ["bath_towel", "hand_towel"], 2, [HumanActionType.DeliverItems])),
        new(
            7,
            "Request one pillow",
            "We need another pillow.",
            Amenities,
            "Confirms that one pillow will be brought to Room 412.",
            $"Names more than one pillow, a different item, or a different room; {NoPromise}.",
            "A pillow will be brought to Room 412.",
            [AITraceOutcome.Answered],
            [RequestStatus, CreateRequest],
            [],
            EvalScenario.RequestedEvents,
            [],
            Agent: GuestServiceController.Task,
            Triaged: triaged,
            Read: read,
            SettledBy: "HumanActionAssigned",
            Verify: Requested("housekeeping.amenities", ["pillow"], 1, [HumanActionType.DeliverItems])),
        new(
            8,
            "Report AC problem",
            "The AC isn't working.",
            "Alex is in house in Room 412. Repairs covers something in the guest's room not working, such as the air conditioning, "
            + "lights, or TV; staff are asked to do it, and no approval or payment is needed.",
            "Acknowledges the problem and says the team will take care of it in Room 412.",
            $"Tells the guest to fix it themselves; names a different room; {NoPromise}.",
            "The team will take care of it in Room 412.",
            [AITraceOutcome.Answered],
            [RequestStatus, CreateRequest],
            [],
            EvalScenario.RequestedEvents,
            [],
            Agent: GuestServiceController.Task,
            Triaged: triaged,
            Read: read,
            SettledBy: "HumanActionAssigned",
            Verify: Requested("maintenance.repair", [null], 1, [HumanActionType.CheckRoom, HumanActionType.RepairEquipment])),
        new(
            9,
            "Request status",
            "Where are my towels?",
            "A few minutes ago Alex asked for 2 bath towels for Room 412. The request is open and has been given to a team member, "
            + "who has not yet accepted or started it. There is no estimate of when the towels will arrive.",
            "Says the towels have been asked for and given to a team member (or are being arranged), without saying when they will arrive.",
            "Gives or implies a time or estimate, such as \"in 10 minutes\" or \"shortly\"; says the towels have been delivered or are on their "
            + "way; names a member of staff; says it has made a new request.",
            "The 2 bath towels for Room 412 have been given to a team member.",
            [AITraceOutcome.Answered],
            [RequestStatus],
            [],
            EvalScenario.AnsweredEvents,
            [],
            AskForTowelsAsync,
            Triaged: triaged,
            Read: read,
            UnexpectedEvents: ["ServiceRequestCreated", "HumanActionRequested"],
            Verify: CitesTheOneRequestAsync),
        new(
            10,
            "Duplicate request protection",
            TwoTowels,
            "A few minutes ago Alex asked for 2 bath towels for Room 412. That request is still open and has been given to a team "
            + "member. Alex has now sent the same request again.",
            "Tells the guest their request for the 2 towels is already with the team, rather than confirming a new delivery.",
            $"Confirms a second, separate delivery of towels; {NoPromise}.",
            "The request for 2 bath towels for Room 412 is already with the team.",
            [AITraceOutcome.Answered],
            [RequestStatus],
            [],
            EvalScenario.RepliedEvents,
            [],
            AskForTowelsAsync,
            Agent: GuestServiceController.Task,
            Triaged: triaged,
            Read: read,
            UnexpectedEvents: ["ServiceRequestCreated", "HumanActionRequested"],
            Verify: CitesTheOneRequestAsync),
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

    /// <summary>
    /// Scenarios 9 and 10's earlier request: two towels, given to a team member. The Fake concierge answers the status
    /// question by citing it.
    /// </summary>
    private static async Task AskForTowelsAsync(EvalHost host, CancellationToken cancellationToken)
    {
        (GuestMessageResponse question, _) = await host.AskAsync(TwoTowels, cancellationToken);
        Guid correlation = (await host.EventsAsync(cancellationToken))
            .Single(message => message.EventType == "GuestMessageReceived" && message.AggregateId == question.Id.Value).CorrelationId;
        await host.FlowUntilAsync(correlation, "HumanActionAssigned", cancellationToken);

        ServiceRequest request = Assert.Single(await OpenRequestsAsync(host, cancellationToken));
        host.Script.Chat.Add(Answer(
            "Your 2 bath towels for Room 412 have been given to a team member, who will bring them to you.",
            new SourceReference("ServiceRequest", request.Id.Value, 1)));
    }

    /// <summary>
    /// Checks the request the message made, and the work asked for it, against what the guest asked for.
    /// </summary>
    private static Func<EvalHost, EvalTurn, CancellationToken, Task<IReadOnlyList<string>>> Requested(
        string service,
        string?[] items,
        int quantity,
        HumanActionType[] work) =>
        async (host, turn, cancellationToken) =>
        {
            (ServiceRequest? request, string? code, IReadOnlyList<HumanAction> asked) = await host.ReadInTenantAsync(async provider =>
            {
                ServiceRequest? made = await provider.GetRequiredService<IServiceRequestRepository>().FindBySourceMessageAsync(turn.Question.Id, cancellationToken);

                if (made is null)
                {
                    return ((ServiceRequest?)null, (string?)null, (IReadOnlyList<HumanAction>)[]);
                }

                Service? offered = await provider.GetRequiredService<IServiceRepository>().GetByIdAsync(made.ServiceId, cancellationToken);
                IReadOnlyList<HumanAction> actions = await provider.GetRequiredService<IHumanActionRepository>().ListForServiceRequestAsync(made.Id, cancellationToken);

                return (made, offered?.Details.Code, actions);
            });

            if (request is null)
            {
                return ["No service request was created from the message."];
            }

            List<string> failures = [];
            ServiceRequestDetails details = request.Details;

            if (code != service)
            {
                failures.Add($"The request is for {code}, not {service}.");
            }

            if (!items.Contains(details.ItemCode))
            {
                failures.Add($"The request is for {details.ItemCode ?? "no item"}, not {string.Join(" or ", items.Select(item => item ?? "no item"))}.");
            }

            if (details.Quantity != quantity)
            {
                failures.Add($"The request is for {details.Quantity}, not {quantity}.");
            }

            if (details.Location.AccommodationUnitId != Seed.Room412)
            {
                failures.Add($"The request is for {details.Location.Label}, not Room 412.");
            }

            if (details.ItemCode is null && string.IsNullOrWhiteSpace(details.Description))
            {
                failures.Add("The request does not describe the problem.");
            }

            if (asked.Count != 1)
            {
                failures.Add($"{asked.Count} pieces of work were asked for, not one.");
            }
            else if (!work.Contains(asked[0].Details.ActionType))
            {
                failures.Add($"The work is {asked[0].Details.ActionType}, not {string.Join(" or ", work)}.");
            }

            SourceReference made = new("ServiceRequest", request.Id.Value, 1);

            if (turn.Trace?.Body.Response?.Citations.Contains(made) != true)
            {
                failures.Add("The reply does not cite the request it made.");
            }

            return failures;
        };

    /// <summary>
    /// Checks that Alex still has just the one request made earlier, and that the reply cites it.
    /// </summary>
    private static async Task<IReadOnlyList<string>> CitesTheOneRequestAsync(EvalHost host, EvalTurn turn, CancellationToken cancellationToken)
    {
        IReadOnlyList<ServiceRequest> open = await OpenRequestsAsync(host, cancellationToken);

        if (open.Count != 1)
        {
            return [$"Alex has {open.Count} open requests, not one."];
        }

        SourceReference earlier = new("ServiceRequest", open[0].Id.Value, 1);

        return turn.Trace?.Body.Response?.Citations.Contains(earlier) == true ? [] : ["The reply does not cite the request made earlier."];
    }

    private static Task<IReadOnlyList<ServiceRequest>> OpenRequestsAsync(EvalHost host, CancellationToken cancellationToken) =>
        host.ReadInTenantAsync(provider => provider.GetRequiredService<IServiceRequestRepository>().ListActiveForStayAsync(Seed.AlexStay, cancellationToken));

    private static FakeExtractionRuleOptions Reading(string says, string output) => new(says, output);

    // The scripted concierge calls its tool first, then answers from the result.
    private static FakeChatRuleOptions Call(string tool, object arguments) =>
        new(CallTool: tool, Arguments: JsonSerializer.Serialize(arguments, ContractJson.Options));

    private static FakeChatRuleOptions Answer(string afterTool, string answer, params SourceReference[] citations) =>
        new(AfterTool: afterTool, Reply: JsonSerializer.Serialize(new { status = "Answered", answer, citations }, ContractJson.Options));

    // Answered from the context, without a tool.
    private static FakeChatRuleOptions Answer(string answer, params SourceReference[] citations) =>
        new(Reply: JsonSerializer.Serialize(new { status = "Answered", answer, citations }, ContractJson.Options));

    private static FakeChatRuleOptions Unknown(string? after = null) =>
        new(AfterTool: after, Reply: """{"status":"Unknown","answer":"","citations":[]}""");
}
