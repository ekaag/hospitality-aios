using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Activities.PropertyEvents.Events;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Capabilities.AITraces.Events;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations.Events;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Menus.Events;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Dining.Restaurants.Events;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.Conversations.Events;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.GuestMessages.Events;
using HospitalityOS.Domain.Guests.Guests.Events;
using HospitalityOS.Domain.Guests.Notifications;
using HospitalityOS.Domain.Guests.Notifications.Events;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Guests.StayContexts.Events;
using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.ExternalReferences.Events;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations.Events;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Operations.HumanActions.Events;
using HospitalityOS.Domain.Operations.StaffMembers;
using HospitalityOS.Domain.Operations.StaffMembers.Events;
using HospitalityOS.Domain.Organizations.Organizations.Events;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.AccommodationTypes.Events;
using HospitalityOS.Domain.Properties.AccommodationUnits.Events;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.Amenities.Events;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.ContentItems.Events;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Hours.Events;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Domain.Properties.Policies.Events;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Properties.Events;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Domain.Properties.Spaces.Events;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.ServiceRequests.Events;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.Domain.Services.Services.Events;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Locations;
using HospitalityOS.SharedKernel.Pricing;
using Xunit;

namespace HospitalityOS.UnitTests.Eventing.TestDoubles;

/// <summary>
/// One realistic instance of every domain event, from the Aurora Grand demo. The contract tests check that this
/// list covers every domain event type, so a new event cannot ship without a published contract and sample.
/// </summary>
public static class DomainEventSamples
{
    public static DateTimeOffset Now { get; } = new(2026, 10, 24, 19, 30, 0, TimeSpan.Zero);

    private static readonly PropertyId auroraGrand = new(Id(1));
    private static readonly OrganizationId auroraHotels = new(Id(2));
    private static readonly SpaceId floor4 = new(Id(3));
    private static readonly SpaceId room412Space = new(Id(4));
    private static readonly AccommodationUnitId room412 = new(Id(5));
    private static readonly AccommodationTypeId deluxeKing = new(Id(6));
    private static readonly AmenityId pool = new(Id(7));
    private static readonly RestaurantId saffron = new(Id(8));
    private static readonly MenuId dinnerMenu = new(Id(9));
    private static readonly ServiceId housekeepingService = new(Id(10));
    private static readonly ServiceRequestId towelRequest = new(Id(11));
    private static readonly HumanActionId deliverTowels = new(Id(12));
    private static readonly StaffMemberId maria = new(Id(13));
    private static readonly GuestId alex = new(Id(14));
    private static readonly StayContextId alexStay = new(Id(15));
    private static readonly ConversationId alexChat = new(Id(16));
    private static readonly GuestMessageId towelMessage = new(Id(17));

    private static readonly PropertyDetails auroraDetails = new(
        "Aurora Grand",
        "Europe/London",
        new PostalAddress("1 Strand", null, "London", null, "WC2N 5HR", "GB"),
        "+44 20 7946 0000",
        "concierge@aurora-grand.example");

    private static readonly ActionLocation room412Location = new("Room 412", room412, room412Space);

    private static readonly OperatingHoursSchedule poolHours = new(
        [new DayHours(DayOfWeek.Monday, [new TimeInterval(new TimeOnly(6, 0), new TimeOnly(22, 0))])],
        [new DateOverride(new DateOnly(2026, 12, 25), [], "Closed for Christmas")],
        [new TemporaryClosure(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4), "Maintenance")]);

    private static readonly MenuDetails dinner = new(
        "Dinner",
        [new MenuItem(new MenuItemId(Id(18)), "Paneer tikka", "Chargrilled paneer", new Money(14.50m, "GBP"), "Mains", [DietaryTag.Vegetarian], true)],
        "Served 18:00-22:30");

    private static readonly ServiceDetails housekeepingDetails = new(
        "housekeeping.amenities",
        "housekeepingService amenities",
        ServiceCategory.Housekeeping,
        "Towels, pillows, toiletries",
        true,
        [new ServiceItem("bath_towel", "Bath towel", 4)],
        TimeSpan.FromMinutes(20));

    private static readonly RequestOrigin guestOrigin = new(alex, alexStay, alexChat, towelMessage);

    private static readonly IDomainEvent[] all =
    [
        new OrganizationCreated(auroraHotels, "Aurora Hotels", Now),
        new OrganizationUpdated(auroraHotels, "Aurora Hotels Group", Now),

        new PropertyCreated(auroraGrand, auroraHotels, auroraDetails, Now),
        new PropertyUpdated(auroraGrand, auroraDetails with { ContactPhone = null }, Now),
        new SpaceCreated(room412Space, auroraGrand, floor4, new SpaceDetails("Room 412", SpaceKind.Room, "412", null, "Fourth floor, left from the lifts"), Now),
        new SpaceUpdated(room412Space, new SpaceDetails("Room 412", SpaceKind.Room, "412"), Now),
        new AccommodationTypeCreated(deluxeKing, auroraGrand, new AccommodationTypeDetails("Deluxe King", 2, "King bed, city view"), Now),
        new AccommodationTypeUpdated(deluxeKing, new AccommodationTypeDetails("Deluxe King", 3), Now),
        new AccommodationUnitCreated(room412, auroraGrand, deluxeKing, room412Space, "412", Now),
        new AccommodationUnitUpdated(room412, deluxeKing, "412A", Now),
        new AmenityCreated(pool, auroraGrand, new AmenityDetails("Pool", AmenityCategory.Pool, "Heated indoor pool"), floor4, Now),
        new AmenityUpdated(pool, new AmenityDetails("Pool", AmenityCategory.Pool), null, Now),
        new ContentItemCreated(new ContentItemId(Id(19)), auroraGrand, new ContentItemDetails(ContentType.Faq, "Wi-Fi", "Join Aurora-Guest.", new SubjectReference(SubjectKind.Property, Id(1)), ["connectivity", "wifi"]), ContentStatus.Draft, Now),
        new ContentItemUpdated(new ContentItemId(Id(19)), new ContentItemDetails(ContentType.Faq, "Wi-Fi", "Join Aurora-Guest."), ContentStatus.Published, Now),
        new OperatingHoursCreated(new OperatingHoursId(Id(20)), auroraGrand, new SubjectReference(SubjectKind.Amenity, pool.Value), poolHours, Now),
        new OperatingHoursUpdated(new OperatingHoursId(Id(20)), OperatingHoursSchedule.WeeklyOnly([new DayHours(DayOfWeek.Saturday, [TimeInterval.AllDay])]), Now),
        new PolicyCreated(new PolicyId(Id(21)), auroraGrand, new PolicyDetails(PolicyType.Children, "Children in the gym", "Guests under 16 must be accompanied by an adult.", new DateOnly(2026, 1, 1), null, new SubjectReference(SubjectKind.Amenity, Id(22))), PolicyStatus.Draft, Now),
        new PolicyUpdated(new PolicyId(Id(21)), new PolicyDetails(PolicyType.Children, "Children in the gym", "Guests under 16 must be accompanied by an adult."), PolicyStatus.Active, Now),

        new RestaurantCreated(saffron, auroraGrand, new RestaurantDetails("Saffron", "Indian", "Modern Indian dining", floor4), Now),
        new RestaurantUpdated(saffron, new RestaurantDetails("Saffron"), Now),
        new MenuCreated(dinnerMenu, auroraGrand, saffron, dinner, Now),
        new MenuUpdated(dinnerMenu, dinner with { Description = null }, Now),

        new PropertyEventCreated(new PropertyEventId(Id(23)), auroraGrand, new PropertyEventDetails("Jazz night", PropertyEventCategory.Entertainment, EventAudience.Everyone, new DateTime(2026, 10, 24, 20, 0, 0), new DateTime(2026, 10, 24, 22, 0, 0), "Live trio in the lobby bar", floor4, new Money(10m, "GBP"), true), PropertyEventStatus.Scheduled, Now),
        new PropertyEventUpdated(new PropertyEventId(Id(23)), new PropertyEventDetails("Jazz night", PropertyEventCategory.Entertainment, EventAudience.AdultsOnly, new DateTime(2026, 10, 24, 20, 0, 0), new DateTime(2026, 10, 24, 22, 0, 0)), PropertyEventStatus.Cancelled, Now),

        new ServiceCreated(housekeepingService, auroraGrand, housekeepingDetails, Now),
        new ServiceUpdated(housekeepingService, housekeepingDetails with { Items = null, TargetCompletion = null }, Now),
        new ServiceRequestCreated(towelRequest, auroraGrand, housekeepingService, ServiceCategory.Housekeeping, new ServiceRequestDetails(room412Location, "bath_towel", 2), guestOrigin, Now.AddMinutes(20), Now),
        new ServiceRequestUpdated(towelRequest, ServiceRequestStatus.Exception, "Worker could not access the room", Now),
        new ServiceRequestCompleted(towelRequest, auroraGrand, guestOrigin, Now),

        new HumanActionRequested(deliverTowels, auroraGrand, towelRequest, HumanActionType.DeliverItems, HumanActionPriority.Normal, room412Location, Now.AddMinutes(20), ["housekeeping"], Now),
        new HumanActionAssigned(deliverTowels, maria, "HumanActionRouter", Now),
        new HumanActionAccepted(deliverTowels, maria, Now),
        new HumanActionStarted(deliverTowels, maria, towelRequest, Now),
        new HumanActionCompleted(deliverTowels, maria, towelRequest, Now),
        new HumanActionFailed(deliverTowels, maria, towelRequest, "Guest not in room; do not disturb sign", Now),
        new HumanActionCancelled(deliverTowels, towelRequest, "Guest cancelled the request", Now),
        new HumanActionExpired(deliverTowels, null, Now),
        new StaffMemberCreated(maria, auroraGrand, new StaffMemberDetails("Maria Lopez", ["housekeeping"]), true, Now),
        new StaffMemberUpdated(maria, new StaffMemberDetails("Maria Lopez", ["housekeeping", "minibar"]), Now),
        new StaffMemberDutyChanged(maria, auroraGrand, false, Now),

        new GuestRegistered(alex, Now),
        new GuestProfileUpdated(alex, Now),
        new StayContextCreated(alexStay, auroraGrand, alex, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 27), room412, StayStatus.Reserved, Now),
        new StayContextUpdated(alexStay, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 28), room412, StayStatus.InHouse, Now),
        new ConversationStarted(alexChat, auroraGrand, ConversationChannel.Web, Now),
        new ConversationUpdated(alexChat, alex, alexStay, ConversationStatus.Open, Now),
        new GuestMessageReceived(towelMessage, alexChat, auroraGrand, alex, alexStay, Now),
        new GuestMessageSent(new GuestMessageId(Id(24)), alexChat, auroraGrand, MessageSender.Concierge, Now),
        new GuestMessageTriaged(towelMessage, alexChat, auroraGrand, alex, alexStay, MessageIntent.ServiceRequest, Now),
        new GuestNotificationRequested(new GuestNotificationId(Id(30)), auroraGrand, alexChat, alex, ConversationChannel.Web, "ServiceRequest", towelRequest.Value, Now),
        new GuestNotificationSent(new GuestNotificationId(Id(30)), alexChat, "MockMessaging", "mock-msg-000001", Now),
        new GuestNotificationFailed(new GuestNotificationId(Id(31)), alexChat, "integration.rejected", Now),
        new GuestQuestionAnswered(towelMessage, new GuestMessageId(Id(24)), alexChat, auroraGrand, AnswerOutcome.Answered, Now),
        new CapabilityInvocationRequested(new CapabilityInvocationId(Id(25)), "GuestService.CreateRequest", 1, Now),
        new CapabilityInvocationSucceeded(new CapabilityInvocationId(Id(25)), "GuestService.CreateRequest", 1, Now),
        new CapabilityInvocationFailed(new CapabilityInvocationId(Id(26)), "HumanAction.Complete", 1, "humanaction.not_assigned", Now),
        new AITraceCreated(new AITraceId(Id(29)), auroraGrand, "concierge.answer", "GuestMessage", towelMessage.Value, AITraceOutcome.NoAuthoritativeSource, Now),
        new IntegrationConfigurationCreated(new IntegrationConfigurationId(Id(27)), auroraGrand, ConnectorKind.Pms, "MockPms", IntegrationStatus.Active, Now),
        new IntegrationConfigurationUpdated(new IntegrationConfigurationId(Id(27)), ConnectorKind.Pms, "OperaCloud", IntegrationStatus.Disabled, Now),
        new ExternalReferenceCreated(new ExternalReferenceId(Id(28)), auroraGrand, "MockPms", "StayContext", "RES-0412", alexStay.Value, Now),
    ];

    public static IReadOnlyList<IDomainEvent> All => all;

    public static TheoryData<string> Names => new(all.Select(domainEvent => domainEvent.GetType().Name));

    public static IDomainEvent Get(string name) => all.Single(domainEvent => domainEvent.GetType().Name == name);

    public static Guid Id(int seed) => Guid.Parse($"01890a5d-ac96-774b-bcce-{seed:D12}");
}
