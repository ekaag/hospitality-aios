using System.Text.Json;
using HospitalityOS.Adapters.Local.Inference;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Serialization;
using Mysore = HospitalityOS.Seeding.MysoreGrand;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.Api.Seeding;

/// <summary>
/// What the Fake models play locally against the reference hotels (J3), the Aurora Grand and the Mysore Grand, so the
/// guest chat answers and makes requests without a provider account: triage by keywords, requests read for the seed's services and items, and the concierge calling the
/// tool a question needs, then answering from the seed's records and citing them as retrieved. Each hotel's answers apply
/// only when the concierge's context is that hotel's (its property ID is in the prompt). Anything else, and status
/// checks (their requests are made at run time, so a script cannot cite them), gets the concierge's "no confirmed
/// information" answer. It is registered with the seed; the launch profile points the <c>fake</c> provider at it
/// (<c>Inference:Providers:fake:Settings:Script</c>). Tests and evaluation runs use scripts of their own.
/// </summary>
internal static class ReferenceHotelsScript
{
    public const string Name = "reference-hotels";

    // The labels intent.classify chooses from (GuestTriage's).
    private const string Question = "question";
    private const string ServiceRequest = "service_request";
    private const string RequestStatus = "request_status";

    private static readonly (string Words, int Quantity)[] quantities =
    [
        ("one", 1), ("1", 1), ("two", 2), ("2", 2), ("three", 3), ("3", 3), ("four", 4), ("4", 4),
    ];

    // Each item as guests name it, the more specific first ("hand towel" before "towel").
    private static readonly (string Says, string ItemCode)[] items =
    [
        ("hand towel", "hand_towel"),
        ("bath towel", "bath_towel"),
        ("towel", "bath_towel"),
        ("pillow", "pillow"),
        ("toiletries", "toiletries_kit"),
        ("toiletry", "toiletries_kit"),
    ];

    private static readonly (string Says, string Problem)[] repairs =
    [
        ("air con", "The air conditioning is not working"),
        ("aircon", "The air conditioning is not working"),
        ("heating", "The heating is not working"),
        ("lights", "The lights are not working"),
        ("not working", "Something in the room is not working"),
        ("broken", "Something in the room is broken"),
    ];

    private static readonly string property = Seed.PropertyId.Value.ToString();
    private static readonly string mysore = Mysore.PropertyId.Value.ToString();

    // Versions as the seed leaves them: created once; policies activated and content published, once more each.
    private static readonly SourceReference poolHours = new("OperatingHours", Seed.PoolHours, 1);
    private static readonly SourceReference gymHours = new("OperatingHours", Seed.GymHours, 1);
    private static readonly SourceReference gymChildren = new("Policy", Seed.GymChildrenPolicy, 2);
    private static readonly SourceReference poolChildren = new("Policy", Seed.PoolChildrenPolicy, 2);
    private static readonly SourceReference pets = new("Policy", Seed.PetsPolicy, 2);
    private static readonly SourceReference checkInCheckOut = new("Policy", Seed.CheckInCheckOutPolicy, 2);
    private static readonly SourceReference smoking = new("Policy", Seed.SmokingPolicy, 2);
    private static readonly SourceReference wiFi = new("ContentItem", Seed.WiFiContent, 2);
    private static readonly SourceReference parking = new("ContentItem", Seed.ParkingContent, 2);
    private static readonly SourceReference saffronDinner = new("Menu", Seed.SaffronDinner, 1);
    private static readonly SourceReference saffron = new("Restaurant", Seed.Saffron, 1);
    private static readonly SourceReference harbourGrill = new("Restaurant", Seed.HarbourGrill, 1);
    private static readonly SourceReference jazzNight = new("PropertyEvent", Seed.JazzNight, 1);

    private static readonly SourceReference mysorePoolHours = new("OperatingHours", Mysore.PoolHours, 1);
    private static readonly SourceReference mysoreSpaHours = new("OperatingHours", Mysore.SpaHours, 1);
    private static readonly SourceReference mysorePoolChildren = new("Policy", Mysore.PoolChildrenPolicy, 2);
    private static readonly SourceReference mysorePets = new("Policy", Mysore.PetsPolicy, 2);
    private static readonly SourceReference mysoreCheckInCheckOut = new("Policy", Mysore.CheckInCheckOutPolicy, 2);
    private static readonly SourceReference mysoreSmoking = new("Policy", Mysore.SmokingPolicy, 2);
    private static readonly SourceReference mysoreDressCode = new("Policy", Mysore.DressCodePolicy, 2);
    private static readonly SourceReference mysoreWiFi = new("ContentItem", Mysore.WiFiContent, 2);
    private static readonly SourceReference mysoreArrival = new("ContentItem", Mysore.ArrivalContent, 2);
    private static readonly SourceReference mysoreIllumination = new("ContentItem", Mysore.IlluminationContent, 2);
    private static readonly SourceReference ambariDinner = new("Menu", Mysore.AmbariDinner, 1);
    private static readonly SourceReference ambari = new("Restaurant", Mysore.Ambari, 1);
    private static readonly SourceReference chamundiTerrace = new("Restaurant", Mysore.ChamundiTerrace, 1);
    private static readonly SourceReference illuminationWalk = new("PropertyEvent", Mysore.IlluminationWalk, 1);
    private static readonly SourceReference carnaticEvening = new("PropertyEvent", Mysore.CarnaticEvening, 1);

    // What the concierge answers at the Mysore Grand, the more specific first.
    private static readonly Topic[] mysoreTopics =
    [
        new(
            ["child", "kid", "year-old", "year old"],
            "Property_GetPolicy",
            new { propertyId = mysore, type = "Children" },
            "Children under 14 must be supervised by an adult in the courtyard pool at all times.",
            [mysorePoolChildren]),
        new(["pool", "swim"], "Property_GetOperatingHours", new { propertyId = mysore, subject = "Amenity", subjectId = Mysore.Pool }, "The courtyard pool is open daily from 06:30 to 20:00.", [mysorePoolHours]),
        new(["spa", "massage", "ayurved"], "Property_GetOperatingHours", new { propertyId = mysore, subject = "Amenity", subjectId = Mysore.Spa }, "The Ayurveda spa is open daily from 09:00 to 21:00.", [mysoreSpaHours]),
        new(
            ["check-out", "check out", "checkout", "check-in", "check in", "checkin"],
            "Property_GetPolicy",
            new { propertyId = mysore, type = "CheckInCheckOut" },
            "Check-in is from 14:00 and check-out is by 12:00. You can ask reception for a late check-out until 15:00, subject to availability.",
            [mysoreCheckInCheckOut]),
        new(["dog", "pets"], "Property_GetPolicy", new { propertyId = mysore, type = "Pets" }, "Pets are not allowed at the Mysore Grand, except assistance dogs.", [mysorePets]),
        new(["smok", "vape"], "Property_GetPolicy", new { propertyId = mysore, type = "Smoking" }, "The Mysore Grand is non-smoking throughout, including the terrace; an INR 10,000 cleaning charge applies.", [mysoreSmoking]),
        new(["dress", "wear"], "Property_GetPolicy", new { propertyId = mysore, type = "DressCode" }, "Smart casual in Durbar Hall after 19:00; please cover your shoulders for the Carnatic music evenings.", [mysoreDressCode]),
        new(["wifi", "wi-fi", "internet"], "Property_SearchContent", new { propertyId = mysore, query = "wifi" }, "Join the MysoreGrand-Guest network; the password is printed on your key card sleeve.", [mysoreWiFi]),
        new(["parking", "park my", "valet", "arrive", "station"], "Property_SearchContent", new { propertyId = mysore, query = "parking" }, "Drive in through the Elephant Gate on Palace Road; valet parking is free for guests. Mysuru Junction station is 10 minutes away.", [mysoreArrival]),
        new(
            ["palace", "illuminat", "lit up", "lights"],
            "Property_SearchContent",
            new { propertyId = mysore, query = "palace illumination" },
            "Mysore Palace is lit by nearly 100,000 bulbs on Sundays and public holidays from 19:00 to 19:45, and every night during Dasara. The roof terrace has the best view.",
            [mysoreIllumination]),
        new(
            ["vegetarian", "vegan"],
            "Dining_GetMenu",
            new { restaurantId = Mysore.Ambari, dietary = new[] { "Vegetarian" } },
            "At Ambari you can have Mysore masala dosa (INR 280), bisi bele bath (INR 320), Mysore pak (INR 180), or filter coffee (INR 120), all vegetarian.",
            [ambariDinner]),
        new(
            ["restaurant", "dinner", "lunch", "food", "hungry"],
            "Dining_SearchRestaurants",
            new { propertyId = mysore },
            "Ambari serves Mysore and Karnataka classics, and Chamundi Terrace serves tandoor and grill on the roof, facing Chamundi Hill.",
            [ambari, chamundiTerrace]),
        new(
            ["tonight", "event", "to do", "music"],
            "Events_SearchPropertyEvents",
            new { propertyId = mysore },
            "Tonight there's a palace illumination walk from 18:45, meeting in Durbar Hall, then a Carnatic music evening in Durbar Hall from 20:30 to 22:00, free for guests.",
            [illuminationWalk, carnaticEvening]),
    ];

    // What the concierge answers at the Aurora Grand, the more specific first: a question is answered by the first topic
    // it mentions.
    private static readonly Topic[] topics =
    [
        new(
            ["child", "kid", "year-old", "year old"],
            "Property_GetPolicy",
            new { propertyId = property, type = "Children" },
            "Children aged 12 to 15 may use the gym with an adult; under-12s may not. In the pool hall, children under 16 must be "
                + "supervised by an adult, and children may swim from 07:00 to 19:00.",
            [gymChildren, poolChildren]),
        new(["pool", "swim"], "Property_GetOperatingHours", new { propertyId = property, subject = "Amenity", subjectId = Seed.Pool }, "The pool is open daily from 07:00 to 22:00.", [poolHours]),
        new(["gym", "fitness"], "Property_GetOperatingHours", new { propertyId = property, subject = "Amenity", subjectId = Seed.Gym }, "The gym is open daily from 06:00 to 23:00.", [gymHours]),
        new(
            ["check-out", "check out", "checkout", "check-in", "check in", "checkin"],
            "Property_GetPolicy",
            new { propertyId = property, type = "CheckInCheckOut" },
            "Check-in is from 15:00 and check-out is by 11:00. You can ask reception for a late check-out until 13:00, subject to availability.",
            [checkInCheckOut]),
        new(["dog", "pets"], "Property_GetPolicy", new { propertyId = property, type = "Pets" }, "Dogs up to 15 kg are welcome for GBP 30 per night; please let us know before you arrive.", [pets]),
        new(["smok", "vape"], "Property_GetPolicy", new { propertyId = property, type = "Smoking" }, "The Aurora Grand is non-smoking throughout, including balconies; a GBP 250 cleaning charge applies.", [smoking]),
        new(["wifi", "wi-fi", "internet"], "Property_SearchContent", new { propertyId = property, query = "wifi" }, "Join the Aurora-Guest network; the password is printed on your key card sleeve.", [wiFi]),
        new(["parking", "park my", "valet"], "Property_SearchContent", new { propertyId = property, query = "parking" }, "Valet parking is at the Savoy Street entrance, GBP 45 per night. Leave your keys with the doorman.", [parking]),
        new(
            ["vegetarian", "vegan"],
            "Dining_GetMenu",
            new { restaurantId = Seed.Saffron, dietary = new[] { "Vegetarian" } },
            "At Saffron you can have paneer tikka (GBP 12.50) or mango kulfi (GBP 8), both vegetarian.",
            [saffronDinner]),
        new(
            ["restaurant", "dinner", "lunch", "food", "hungry"],
            "Dining_SearchRestaurants",
            new { propertyId = property },
            "Saffron serves modern Indian food at dinner, and Harbour Grill serves seafood at lunch.",
            [saffron, harbourGrill]),
        new(
            ["tonight", "event", "to do", "jazz", "music"],
            "Events_SearchPropertyEvents",
            new { propertyId = property },
            "Jazz night is on in the lobby bar from 20:00 to 23:00 tonight, and it's free.",
            [jazzNight]),
    ];

    public static FakeScriptOptions Create()
    {
        FakeScriptOptions script = new();

        foreach (string says in new[] { "where are my", "where is my", "status", "any update", "still waiting" })
        {
            script.Classification.Add(new(says, RequestStatus));
        }

        foreach (string says in items.Select(item => item.Says).Concat(repairs.Select(repair => repair.Says)))
        {
            script.Classification.Add(new(says, ServiceRequest));
        }

        // An empty phrase is in every message: anything else is a question.
        script.Classification.Add(new("", Question));

        foreach ((string says, string itemCode) in items)
        {
            foreach ((string words, int quantity) in quantities)
            {
                script.Extraction.Add(new($"{words} more {says}", Reading(itemCode, quantity, more: true)));
                script.Extraction.Add(new($"{words} extra {says}", Reading(itemCode, quantity, more: true)));
                script.Extraction.Add(new($"{words} {says}", Reading(itemCode, quantity, more: false)));
            }

            script.Extraction.Add(new($"another {says}", Reading(itemCode, 1, more: true)));
            script.Extraction.Add(new($"more {says}", Reading(itemCode, null, more: true)));
            script.Extraction.Add(new(says, Reading(itemCode, null, more: false)));
        }

        foreach ((string says, string problem) in repairs)
        {
            script.Extraction.Add(new(says, Serialize(new { understood = true, serviceCode = "maintenance.repair", itemCode = (string?)null, quantity = (int?)null, description = problem, more = false })));
        }

        // Answer once the topic's tool has answered; until then, call it. Each hotel's topics apply in its context only.
        foreach ((string hotel, Topic[] answers) in new[] { (property, topics), (mysore, mysoreTopics) })
        {
            foreach (Topic topic in answers)
            {
                foreach (string says in topic.Says)
                {
                    script.Chat.Add(new(WhenUserSays: says, AfterTool: topic.Tool, Reply: Serialize(new { status = "Answered", answer = topic.Answer, citations = topic.Citations }), WhenPromptContains: hotel));
                    script.Chat.Add(new(WhenUserSays: says, CallTool: topic.Tool, Arguments: Serialize(topic.Arguments), WhenPromptContains: hotel));
                }
            }
        }

        script.Chat.Add(new(Reply: """{"status":"Unknown","answer":"","citations":[]}"""));

        return script;
    }

    private static string Reading(string itemCode, int? quantity, bool more) =>
        Serialize(new { understood = true, serviceCode = "housekeeping.amenities", itemCode, quantity, description = (string?)null, more });

    private static string Serialize(object value) => JsonSerializer.Serialize(value, ContractJson.Options);

    /// <summary>
    /// A question the concierge answers: when the guest says any of <paramref name="Says"/>, it calls
    /// <paramref name="Tool"/> with <paramref name="Arguments"/>, then answers citing what the tool returned.
    /// </summary>
    private sealed record Topic(string[] Says, string Tool, object Arguments, string Answer, SourceReference[] Citations);
}
