using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <summary>
/// Builders for valid aggregates at the Aurora Grand. <c>seed</c> makes IDs and unique values distinct, so a test
/// can build several aggregates of one type; tests get a fresh store, so seeds only need to be unique per test.
/// </summary>
internal static class TestData
{
    public static TenantId TenantA { get; } = new(Id(1));

    public static TenantId TenantB { get; } = new(Id(2));

    public static OrganizationId OrganizationId { get; } = new(Id(3));

    public static PropertyId PropertyId { get; } = new(Id(4));

    public static PropertyId OtherPropertyId { get; } = new(Id(5));

    public static StaffMemberId Maria { get; } = new(Id(6));

    public static DateTimeOffset Now { get; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static Guid Id(int seed) => Guid.Parse($"01890a5d-ac96-774b-bcce-{seed:D12}");

    public static Organization Organization(TenantId tenantId, int seed) =>
        Domain.Organizations.Organizations.Organization.Create(new OrganizationId(Id(1000 + seed)), tenantId, $"Aurora Hotels {seed}", Now).Value;

    public static Property Property(TenantId tenantId, int seed) =>
        Domain.Properties.Properties.Property.Create(
            new PropertyId(Id(2000 + seed)),
            tenantId,
            OrganizationId,
            new PropertyDetails("Aurora Grand", "Europe/London", new PostalAddress("1 Strand", null, "London", null, "WC2N 5HR", "GB")),
            Now).Value;

    public static Space Space(TenantId tenantId, int seed, PropertyId? propertyId = null) =>
        Domain.Properties.Spaces.Space.Create(new SpaceId(Id(3000 + seed)), tenantId, propertyId ?? PropertyId, null, new SpaceDetails($"Floor {seed}", SpaceKind.Floor), Now).Value;

    public static AccommodationType AccommodationType(TenantId tenantId, int seed, PropertyId? propertyId = null) =>
        Domain.Properties.AccommodationTypes.AccommodationType.Create(
            new AccommodationTypeId(Id(4000 + seed)),
            tenantId,
            propertyId ?? PropertyId,
            new AccommodationTypeDetails("Deluxe King", 2),
            Now).Value;

    public static AccommodationUnit AccommodationUnit(TenantId tenantId, int seed, string? unitNumber = null, PropertyId? propertyId = null) =>
        Domain.Properties.AccommodationUnits.AccommodationUnit.Create(
            new AccommodationUnitId(Id(5000 + seed)),
            AccommodationType(tenantId, seed, propertyId),
            Space(tenantId, seed, propertyId),
            unitNumber ?? $"{400 + seed}",
            Now).Value;

    public static SubjectReference Pool { get; } = new(SubjectKind.Amenity, Id(6999));

    public static OperatingHours OperatingHours(TenantId tenantId, int seed, SubjectReference? subject = null, PropertyId? propertyId = null) =>
        Domain.Properties.Hours.OperatingHours.Create(
            new OperatingHoursId(Id(6000 + seed)),
            tenantId,
            propertyId ?? PropertyId,
            subject ?? new SubjectReference(SubjectKind.Amenity, Id(6500 + seed)),
            Weekly(DayOfWeek.Monday),
            Now).Value;

    public static OperatingHoursSchedule Weekly(params DayOfWeek[] days) =>
        OperatingHoursSchedule.WeeklyOnly([.. days.Select(day => new DayHours(day, [new TimeInterval(new TimeOnly(6, 0), new TimeOnly(22, 0))]))]);

    public static Amenity Amenity(TenantId tenantId, int seed) =>
        Domain.Properties.Amenities.Amenity.Create(new AmenityId(Id(7000 + seed)), tenantId, PropertyId, new AmenityDetails("Pool", AmenityCategory.Pool), null, Now).Value;

    public static Policy Policy(TenantId tenantId, int seed) =>
        Domain.Properties.Policies.Policy.Create(
            new PolicyId(Id(8000 + seed)),
            tenantId,
            PropertyId,
            new PolicyDetails(PolicyType.Children, "Children in the gym", "Guests under 16 must be accompanied by an adult."),
            Now).Value;

    public static ContentItem ContentItem(TenantId tenantId, int seed) =>
        Domain.Properties.ContentItems.ContentItem.Create(
            new ContentItemId(Id(9000 + seed)),
            tenantId,
            PropertyId,
            new ContentItemDetails(ContentType.Faq, "Wi-Fi", "Join the Aurora-Guest network."),
            Now).Value;

    public static Restaurant Restaurant(TenantId tenantId, int seed) =>
        Domain.Dining.Restaurants.Restaurant.Create(new RestaurantId(Id(10000 + seed)), tenantId, PropertyId, new RestaurantDetails("Saffron"), Now).Value;

    public static MenuItemId PaneerTikka { get; } = new(Id(11999));

    public static Menu Menu(TenantId tenantId, int seed) =>
        Domain.Dining.Menus.Menu.Create(
            new MenuId(Id(11000 + seed)),
            Restaurant(tenantId, seed),
            new MenuDetails("Dinner", [new MenuItem(PaneerTikka, "Paneer tikka", Dietary: [DietaryTag.Vegetarian])]),
            Now).Value;

    public static PropertyEvent PropertyEvent(TenantId tenantId, int seed) =>
        Domain.Activities.PropertyEvents.PropertyEvent.Create(
            new PropertyEventId(Id(12000 + seed)),
            tenantId,
            PropertyId,
            new PropertyEventDetails("Jazz night", PropertyEventCategory.Entertainment, EventAudience.Everyone, new DateTime(2026, 10, 24, 20, 0, 0), new DateTime(2026, 10, 24, 22, 0, 0)),
            Now).Value;

    public static Service Service(TenantId tenantId, int seed, string? code = null, PropertyId? propertyId = null) =>
        Domain.Services.Services.Service.Create(
            new ServiceId(Id(13000 + seed)),
            tenantId,
            propertyId ?? PropertyId,
            new ServiceDetails(
                code ?? $"housekeeping.amenities{seed}",
                "Housekeeping amenities",
                ServiceCategory.Housekeeping,
                Items: [new ServiceItem("bath_towel", "Bath towel", 4)],
                TargetCompletion: TimeSpan.FromMinutes(20)),
            Now).Value;

    public static ServiceRequest ServiceRequest(
        TenantId tenantId,
        int seed,
        GuestMessageId? sourceMessageId = null,
        StayContextId? stayContextId = null,
        DateTimeOffset? createdAt = null) =>
        Domain.Services.ServiceRequests.ServiceRequest.Create(
            new ServiceRequestId(Id(14000 + seed)),
            Service(tenantId, seed),
            new ServiceRequestDetails(new ActionLocation("Room 412"), "bath_towel", 2),
            new RequestOrigin(StayContextId: stayContextId, SourceMessageId: sourceMessageId),
            createdAt ?? Now).Value;

    public static HumanAction HumanAction(TenantId tenantId, int seed, ServiceRequestId? serviceRequestId = null, DateTimeOffset? createdAt = null) =>
        Domain.Operations.HumanActions.HumanAction.Request(
            new HumanActionId(Id(15000 + seed)),
            tenantId,
            PropertyId,
            new HumanActionRequest(
                HumanActionType.DeliverItems,
                "Deliver 2 bath towels",
                new ActionLocation("Room 412"),
                "Guest requested towels via concierge",
                "GuestServiceController",
                Sla: TimeSpan.FromMinutes(20),
                Items: [new ActionItem("Bath towel", 2, "bath_towel")],
                ServiceRequestId: serviceRequestId),
            createdAt ?? Now).Value;

    public static Guest Guest(TenantId tenantId, int seed) =>
        Domain.Guests.Guests.Guest.Register(new GuestId(Id(16000 + seed)), tenantId, new GuestProfile("Alex", "en"), Now).Value;

    public static AccommodationUnitId Room412 { get; } = new(Id(17999));

    public static StayContext StayContext(TenantId tenantId, int seed, Guest? guest = null, PropertyId? propertyId = null) =>
        Domain.Guests.StayContexts.StayContext.Create(
            new StayContextId(Id(17000 + seed)),
            guest ?? Guest(tenantId, seed),
            propertyId ?? PropertyId,
            new DateOnly(2026, 10, 24),
            new DateOnly(2026, 10, 27),
            Room412,
            Now).Value;

    public static Conversation Conversation(TenantId tenantId, int seed) =>
        Domain.Guests.Conversations.Conversation.Start(new ConversationId(Id(18000 + seed)), tenantId, PropertyId, ConversationChannel.Web, Now).Value;

    public static GuestMessage GuestMessage(TenantId tenantId, int seed, Conversation? conversation = null, string? clientMessageId = null, DateTimeOffset? sentAt = null) =>
        Domain.Guests.GuestMessages.GuestMessage.Record(
            new GuestMessageId(Id(19000 + seed)),
            conversation ?? Conversation(tenantId, seed),
            MessageSender.Guest,
            $"Message {seed}",
            clientMessageId,
            sentAt ?? Now).Value;
}
