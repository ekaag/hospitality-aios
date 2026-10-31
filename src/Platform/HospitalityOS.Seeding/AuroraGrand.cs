using System.Globalization;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Seeding;

/// <summary>
/// The Aurora Grand, London: the reference property the demos (spec §43), end-to-end tests, and AI evaluations run
/// against. Its records have fixed IDs, so they can be named in requests, tokens, and settings; they read
/// <c>aa000000-0000-4000-8000-0000000NNNNN</c>, rooms ending in their number. Dated data (tonight's events, Alex's stay)
/// is relative to the day the seed first runs, in the property's time zone.
/// </summary>
public static class AuroraGrand
{
    /// <summary>
    /// The property's IANA time zone.
    /// </summary>
    public const string TimeZone = "Europe/London";

    /// <summary>
    /// The Aurora Grand's tenant. A staff token for it carries this as <c>tenant_id</c>.
    /// </summary>
    public static TenantId TenantId { get; } = new(Id(1));

    /// <summary>
    /// Aurora Hotels, the organization owning the property.
    /// </summary>
    public static OrganizationId OrganizationId { get; } = new(Id(2));

    /// <summary>
    /// The Aurora Grand.
    /// </summary>
    public static PropertyId PropertyId { get; } = new(Id(3));

    /// <summary>
    /// The main building (space).
    /// </summary>
    public static SpaceId MainBuilding { get; } = new(Id(10));

    /// <summary>
    /// The lobby, in the main building.
    /// </summary>
    public static SpaceId Lobby { get; } = new(Id(11));

    /// <summary>
    /// The lobby bar, where jazz night is held.
    /// </summary>
    public static SpaceId LobbyBar { get; } = new(Id(12));

    /// <summary>
    /// The leisure level (lower ground floor), with the pool hall and fitness studio.
    /// </summary>
    public static SpaceId LeisureLevel { get; } = new(Id(13));

    /// <summary>
    /// The pool hall, where the pool is.
    /// </summary>
    public static SpaceId PoolHall { get; } = new(Id(14));

    /// <summary>
    /// The fitness studio, where the gym is.
    /// </summary>
    public static SpaceId FitnessStudio { get; } = new(Id(15));

    /// <summary>
    /// The fourth floor, with rooms 412 and 415.
    /// </summary>
    public static SpaceId Floor4 { get; } = new(Id(16));

    /// <summary>
    /// Saffron's dining room.
    /// </summary>
    public static SpaceId SaffronDiningRoom { get; } = new(Id(17));

    /// <summary>
    /// Harbour Grill's dining room.
    /// </summary>
    public static SpaceId HarbourGrillDiningRoom { get; } = new(Id(18));

    /// <summary>
    /// The space of room 412.
    /// </summary>
    public static SpaceId Room412Space { get; } = new(Id(1412));

    /// <summary>
    /// The space of room 415.
    /// </summary>
    public static SpaceId Room415Space { get; } = new(Id(1415));

    /// <summary>
    /// The Deluxe King room type.
    /// </summary>
    public static Guid DeluxeKing { get; } = Id(20);

    /// <summary>
    /// Room 412, where guest Alex is staying (Demo 5).
    /// </summary>
    public static AccommodationUnitId Room412 { get; } = new(Id(412));

    /// <summary>
    /// Room 415, vacant.
    /// </summary>
    public static AccommodationUnitId Room415 { get; } = new(Id(415));

    /// <summary>
    /// The indoor pool (Demo 1: "What time does the pool close tonight?").
    /// </summary>
    public static Guid Pool { get; } = Id(30);

    /// <summary>
    /// The gym.
    /// </summary>
    public static Guid Gym { get; } = Id(31);

    /// <summary>
    /// The pool's operating hours: 07:00 to 22:00 daily, shorter on Christmas Day.
    /// </summary>
    public static Guid PoolHours { get; } = Id(40);

    /// <summary>
    /// The gym's operating hours: 06:00 to 23:00 daily.
    /// </summary>
    public static Guid GymHours { get; } = Id(41);

    /// <summary>
    /// Saffron's operating hours: 18:00 to 23:00 daily.
    /// </summary>
    public static Guid SaffronHours { get; } = Id(42);

    /// <summary>
    /// Harbour Grill's operating hours: lunch and dinner daily.
    /// </summary>
    public static Guid HarbourGrillHours { get; } = Id(43);

    /// <summary>
    /// Children in the gym (Demo 2: "Can my 12-year-old use the gym?").
    /// </summary>
    public static Guid GymChildrenPolicy { get; } = Id(50);

    /// <summary>
    /// Children in the pool.
    /// </summary>
    public static Guid PoolChildrenPolicy { get; } = Id(51);

    /// <summary>
    /// Pets.
    /// </summary>
    public static Guid PetsPolicy { get; } = Id(52);

    /// <summary>
    /// Check-in and check-out times.
    /// </summary>
    public static Guid CheckInCheckOutPolicy { get; } = Id(53);

    /// <summary>
    /// Smoking.
    /// </summary>
    public static Guid SmokingPolicy { get; } = Id(54);

    /// <summary>
    /// The property overview.
    /// </summary>
    public static Guid OverviewContent { get; } = Id(60);

    /// <summary>
    /// The Wi-Fi FAQ.
    /// </summary>
    public static Guid WiFiContent { get; } = Id(61);

    /// <summary>
    /// Parking directions.
    /// </summary>
    public static Guid ParkingContent { get; } = Id(62);

    /// <summary>
    /// Saffron, modern Indian dining.
    /// </summary>
    public static Guid Saffron { get; } = Id(70);

    /// <summary>
    /// Harbour Grill, seafood.
    /// </summary>
    public static Guid HarbourGrill { get; } = Id(71);

    /// <summary>
    /// Saffron's dinner menu, with vegetarian and vegan dishes (Demo 3).
    /// </summary>
    public static Guid SaffronDinner { get; } = Id(80);

    /// <summary>
    /// Harbour Grill's lunch menu.
    /// </summary>
    public static Guid HarbourGrillLunch { get; } = Id(81);

    /// <summary>
    /// Paneer tikka (vegetarian), on Saffron's dinner menu.
    /// </summary>
    public static Guid PaneerTikka { get; } = Id(800);

    /// <summary>
    /// Vegetable biryani (vegan), on Saffron's dinner menu.
    /// </summary>
    public static Guid VegetableBiryani { get; } = Id(801);

    /// <summary>
    /// Lamb rogan josh, on Saffron's dinner menu.
    /// </summary>
    public static Guid LambRoganJosh { get; } = Id(802);

    /// <summary>
    /// Tarka dal (vegan, gluten-free), on Saffron's dinner menu.
    /// </summary>
    public static Guid TarkaDal { get; } = Id(803);

    /// <summary>
    /// Mango kulfi (vegetarian), on Saffron's dinner menu.
    /// </summary>
    public static Guid MangoKulfi { get; } = Id(804);

    /// <summary>
    /// Fish and chips, on Harbour Grill's lunch menu.
    /// </summary>
    public static Guid FishAndChips { get; } = Id(810);

    /// <summary>
    /// Halloumi burger (vegetarian), on Harbour Grill's lunch menu.
    /// </summary>
    public static Guid HalloumiBurger { get; } = Id(811);

    /// <summary>
    /// Seafood linguine, on Harbour Grill's lunch menu.
    /// </summary>
    public static Guid SeafoodLinguine { get; } = Id(812);

    /// <summary>
    /// Jazz night in the lobby bar, tonight (Demo 4: "What's happening tonight?").
    /// </summary>
    public static Guid JazzNight { get; } = Id(90);

    /// <summary>
    /// Wine tasting, tonight (adults only, booking required).
    /// </summary>
    public static Guid WineTasting { get; } = Id(91);

    /// <summary>
    /// Morning yoga, tomorrow.
    /// </summary>
    public static Guid MorningYoga { get; } = Id(92);

    /// <summary>
    /// Kids club, tomorrow afternoon.
    /// </summary>
    public static Guid KidsClub { get; } = Id(93);

    /// <summary>
    /// Towels, pillows, and toiletries delivered to the room (Demo 5).
    /// </summary>
    public static Guid HousekeepingAmenities { get; } = Id(100);

    /// <summary>
    /// Repairs such as a broken air conditioner, requested by description.
    /// </summary>
    public static Guid MaintenanceRepair { get; } = Id(101);

    /// <summary>
    /// Sam Patel, housekeeping, on duty; the subject of Sam's staff token.
    /// </summary>
    public static StaffMemberId SamPatel { get; } = new(Id(300));

    /// <summary>
    /// Jo Okafor, housekeeping, on duty.
    /// </summary>
    public static StaffMemberId JoOkafor { get; } = new(Id(301));

    /// <summary>
    /// Ravi Mehta, maintenance, on duty.
    /// </summary>
    public static StaffMemberId RaviMehta { get; } = new(Id(302));

    /// <summary>
    /// Alex Morgan, the in-house guest in room 412.
    /// </summary>
    public static GuestId Alex { get; } = new(Id(200));

    /// <summary>
    /// Alex's stay, in house in room 412.
    /// </summary>
    public static StayContextId AlexStay { get; } = new(Id(201));

    /// <summary>
    /// Alex's reservation as the property's PMS (the mock) reports it.
    /// </summary>
    public static ExternalId AlexReservation { get; } = new("AG-RES-10412");

    /// <summary>
    /// Alex's guest profile ID in the PMS.
    /// </summary>
    public static ExternalId AlexPmsGuest { get; } = new("AG-GST-5512");

    /// <summary>
    /// The property's calendar date at <paramref name="now"/>.
    /// </summary>
    public static DateOnly LocalToday(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZone)).DateTime);

    /// <summary>
    /// The in-house stays the Aurora Grand's PMS holds, for the mock PMS provider: Alex, arrived
    /// <paramref name="localToday"/> for three nights in room 412.
    /// </summary>
    public static IReadOnlyList<PmsStay> PmsStays(DateOnly localToday) =>
    [
        new PmsStay(AlexReservation, new PmsGuest(AlexPmsGuest, "Alex Morgan", "en"), localToday, AlexDeparture(localToday), "412", StayStatus.InHouse),
    ];

    internal static DateOnly AlexDeparture(DateOnly arrival) => arrival.AddDays(3);

    private static Guid Id(int number) => Guid.Parse($"aa000000-0000-4000-8000-{number.ToString("D12", CultureInfo.InvariantCulture)}", CultureInfo.InvariantCulture);
}
