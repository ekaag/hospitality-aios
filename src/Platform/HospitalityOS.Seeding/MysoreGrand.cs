using System.Globalization;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Seeding;

/// <summary>
/// The Mysore Grand, Mysuru: a heritage palace hotel, the reference tenant's second property, so staff and
/// administrators can switch between hotels. It is configured as fully as the Aurora Grand (spaces, rooms, amenities and
/// hours, policies, content, restaurants and menus, events, services, and a roster), with Ananya Rao in house in room
/// 210. The demos and AI evaluations run against the Aurora Grand. Its records have fixed IDs reading
/// <c>ab000000-0000-4000-8000-0000000NNNNN</c>; it lives in <see cref="AuroraGrand.TenantId"/>.
/// </summary>
public static class MysoreGrand
{
    /// <summary>
    /// The property's IANA time zone.
    /// </summary>
    public const string TimeZone = "Asia/Kolkata";

    /// <summary>
    /// The property's name.
    /// </summary>
    public const string Name = "Mysore Grand";

    /// <summary>
    /// Mysore Heritage Hotels, the organization owning the property.
    /// </summary>
    public static OrganizationId OrganizationId { get; } = new(Id(2));

    /// <summary>
    /// The Mysore Grand.
    /// </summary>
    public static PropertyId PropertyId { get; } = new(Id(3));

    /// <summary>
    /// The palace wing (space), the main building.
    /// </summary>
    public static SpaceId PalaceWing { get; } = new(Id(10));

    /// <summary>
    /// Durbar Hall, the lobby.
    /// </summary>
    public static SpaceId DurbarHall { get; } = new(Id(11));

    /// <summary>
    /// The Ambavilas Lounge, off Durbar Hall.
    /// </summary>
    public static SpaceId AmbavilasLounge { get; } = new(Id(12));

    /// <summary>
    /// The garden level, with the courtyard pool and the spa.
    /// </summary>
    public static SpaceId GardenLevel { get; } = new(Id(13));

    /// <summary>
    /// The courtyard, where the pool is.
    /// </summary>
    public static SpaceId Courtyard { get; } = new(Id(14));

    /// <summary>
    /// The Ayurveda spa.
    /// </summary>
    public static SpaceId SpaPavilion { get; } = new(Id(15));

    /// <summary>
    /// The second floor, with rooms 210 and 214.
    /// </summary>
    public static SpaceId Floor2 { get; } = new(Id(16));

    /// <summary>
    /// Ambari's dining room.
    /// </summary>
    public static SpaceId AmbariDiningRoom { get; } = new(Id(17));

    /// <summary>
    /// The roof terrace, where Chamundi Terrace serves.
    /// </summary>
    public static SpaceId RoofTerrace { get; } = new(Id(18));

    /// <summary>
    /// The space of room 210.
    /// </summary>
    public static SpaceId Room210Space { get; } = new(Id(1210));

    /// <summary>
    /// The space of room 214.
    /// </summary>
    public static SpaceId Room214Space { get; } = new(Id(1214));

    /// <summary>
    /// The Maharaja Suite room type.
    /// </summary>
    public static Guid MaharajaSuite { get; } = Id(20);

    /// <summary>
    /// Room 210.
    /// </summary>
    public static AccommodationUnitId Room210 { get; } = new(Id(210));

    /// <summary>
    /// Room 214.
    /// </summary>
    public static AccommodationUnitId Room214 { get; } = new(Id(214));

    /// <summary>
    /// The courtyard pool.
    /// </summary>
    public static Guid Pool { get; } = Id(30);

    /// <summary>
    /// The Ayurveda spa.
    /// </summary>
    public static Guid Spa { get; } = Id(31);

    /// <summary>
    /// The pool's operating hours: 06:30 to 20:00 daily.
    /// </summary>
    public static Guid PoolHours { get; } = Id(40);

    /// <summary>
    /// The spa's operating hours: 09:00 to 21:00 daily.
    /// </summary>
    public static Guid SpaHours { get; } = Id(41);

    /// <summary>
    /// Ambari's operating hours: breakfast, lunch, and dinner daily.
    /// </summary>
    public static Guid AmbariHours { get; } = Id(42);

    /// <summary>
    /// Chamundi Terrace's operating hours: evenings daily.
    /// </summary>
    public static Guid ChamundiTerraceHours { get; } = Id(43);

    /// <summary>
    /// Children in the pool.
    /// </summary>
    public static Guid PoolChildrenPolicy { get; } = Id(50);

    /// <summary>
    /// Pets.
    /// </summary>
    public static Guid PetsPolicy { get; } = Id(51);

    /// <summary>
    /// Check-in and check-out times.
    /// </summary>
    public static Guid CheckInCheckOutPolicy { get; } = Id(52);

    /// <summary>
    /// Smoking.
    /// </summary>
    public static Guid SmokingPolicy { get; } = Id(53);

    /// <summary>
    /// Dress code in Durbar Hall in the evening.
    /// </summary>
    public static Guid DressCodePolicy { get; } = Id(54);

    /// <summary>
    /// The property overview.
    /// </summary>
    public static Guid OverviewContent { get; } = Id(60);

    /// <summary>
    /// The Wi-Fi FAQ.
    /// </summary>
    public static Guid WiFiContent { get; } = Id(61);

    /// <summary>
    /// Arrival and parking directions.
    /// </summary>
    public static Guid ArrivalContent { get; } = Id(62);

    /// <summary>
    /// When Mysore Palace is illuminated.
    /// </summary>
    public static Guid IlluminationContent { get; } = Id(63);

    /// <summary>
    /// Ambari, Mysore and Karnataka cuisine.
    /// </summary>
    public static Guid Ambari { get; } = Id(70);

    /// <summary>
    /// Chamundi Terrace, tandoor and grill on the roof.
    /// </summary>
    public static Guid ChamundiTerrace { get; } = Id(71);

    /// <summary>
    /// Ambari's dinner menu.
    /// </summary>
    public static Guid AmbariDinner { get; } = Id(80);

    /// <summary>
    /// Chamundi Terrace's dinner menu.
    /// </summary>
    public static Guid ChamundiTerraceDinner { get; } = Id(81);

    /// <summary>
    /// Mysore masala dosa (vegetarian), on Ambari's dinner menu.
    /// </summary>
    public static Guid MysoreMasalaDosa { get; } = Id(800);

    /// <summary>
    /// Bisi bele bath (vegetarian), on Ambari's dinner menu.
    /// </summary>
    public static Guid BisiBeleBath { get; } = Id(801);

    /// <summary>
    /// Chicken ghee roast, on Ambari's dinner menu.
    /// </summary>
    public static Guid ChickenGheeRoast { get; } = Id(802);

    /// <summary>
    /// Mysore pak (vegetarian), on Ambari's dinner menu.
    /// </summary>
    public static Guid MysorePak { get; } = Id(803);

    /// <summary>
    /// Filter coffee (vegetarian), on Ambari's dinner menu.
    /// </summary>
    public static Guid FilterCoffee { get; } = Id(804);

    /// <summary>
    /// Tandoori prawns, on Chamundi Terrace's dinner menu.
    /// </summary>
    public static Guid TandooriPrawns { get; } = Id(810);

    /// <summary>
    /// Paneer tikka (vegetarian), on Chamundi Terrace's dinner menu.
    /// </summary>
    public static Guid PaneerTikka { get; } = Id(811);

    /// <summary>
    /// Mutton pepper fry, on Chamundi Terrace's dinner menu.
    /// </summary>
    public static Guid MuttonPepperFry { get; } = Id(812);

    /// <summary>
    /// A guided walk to see Mysore Palace illuminated, tonight.
    /// </summary>
    public static Guid IlluminationWalk { get; } = Id(90);

    /// <summary>
    /// Carnatic music in Durbar Hall, tonight.
    /// </summary>
    public static Guid CarnaticEvening { get; } = Id(91);

    /// <summary>
    /// Sunrise yoga on the roof terrace, tomorrow.
    /// </summary>
    public static Guid SunriseYoga { get; } = Id(92);

    /// <summary>
    /// A morning excursion up Chamundi Hill, tomorrow (booking required).
    /// </summary>
    public static Guid ChamundiHillExcursion { get; } = Id(93);

    /// <summary>
    /// Towels, pillows, and toiletries delivered to the room.
    /// </summary>
    public static Guid HousekeepingAmenities { get; } = Id(100);

    /// <summary>
    /// Repairs such as a broken air conditioner, requested by description.
    /// </summary>
    public static Guid MaintenanceRepair { get; } = Id(101);

    /// <summary>
    /// Lakshmi Iyer, housekeeping, on duty.
    /// </summary>
    public static StaffMemberId LakshmiIyer { get; } = new(Id(300));

    /// <summary>
    /// Kiran Gowda, housekeeping, on duty.
    /// </summary>
    public static StaffMemberId KiranGowda { get; } = new(Id(301));

    /// <summary>
    /// Arjun Nair, maintenance, on duty.
    /// </summary>
    public static StaffMemberId ArjunNair { get; } = new(Id(302));

    /// <summary>
    /// Ananya Rao, the in-house guest in room 210.
    /// </summary>
    public static GuestId Ananya { get; } = new(Id(200));

    /// <summary>
    /// Ananya's stay, in house in room 210 for two nights.
    /// </summary>
    public static StayContextId AnanyaStay { get; } = new(Id(201));

    /// <summary>
    /// The property's calendar date at <paramref name="now"/>.
    /// </summary>
    public static DateOnly LocalToday(DateTimeOffset now) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZone)).DateTime);

    private static Guid Id(int number) => Guid.Parse($"ab000000-0000-4000-8000-{number.ToString("D12", CultureInfo.InvariantCulture)}", CultureInfo.InvariantCulture);
}
