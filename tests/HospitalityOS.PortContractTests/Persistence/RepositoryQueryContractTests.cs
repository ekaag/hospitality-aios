using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.GuestMessages.Events;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Integrations.ExternalReferences;
using HospitalityOS.Domain.Integrations.IntegrationConfigurations;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Organizations.Organizations;
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
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.PortContractTests.Persistence;

/// <summary>
/// The repositories' lookups beyond <c>GetByIdAsync</c>: matching rules, ordering, limits, and that they read the
/// current tenant's committed data only.
/// </summary>
public abstract class RepositoryQueryContractTests : PersistenceContractTests
{
    [Fact]
    public async Task FindByUnitNumber_IgnoresCaseAndSurroundingSpaces()
    {
        AccommodationUnit unit = TestData.AccommodationUnit(TestData.TenantA, 1, "12A");
        await SaveAsync(unit);
        await using PersistenceSession session = OpenSession();

        AccommodationUnit? found = await session.Get<IAccommodationUnitRepository>().FindByUnitNumberAsync(TestData.PropertyId, " 12a ", Cancellation);

        Assert.Equal(unit.Id, found?.Id);
    }

    [Fact]
    public async Task FindByUnitNumber_InAnotherPropertyOrTenant_ReturnsNull()
    {
        await SaveAsync(TestData.AccommodationUnit(TestData.TenantA, 1, "412"));
        await using PersistenceSession session = OpenSession();
        await using PersistenceSession otherTenant = OpenSession(TestData.TenantB);

        AccommodationUnit? otherProperty = await session.Get<IAccommodationUnitRepository>().FindByUnitNumberAsync(TestData.OtherPropertyId, "412", Cancellation);
        AccommodationUnit? otherTenantUnit = await otherTenant.Get<IAccommodationUnitRepository>().FindByUnitNumberAsync(TestData.PropertyId, "412", Cancellation);

        Assert.Null(otherProperty);
        Assert.Null(otherTenantUnit);
    }

    [Fact]
    public async Task FindByUnitNumber_AddedButNotCommitted_ReturnsNull()
    {
        await using PersistenceSession session = OpenSession();
        IAccommodationUnitRepository units = session.Get<IAccommodationUnitRepository>();
        await units.AddAsync(TestData.AccommodationUnit(TestData.TenantA, 1, "412"), Cancellation);

        AccommodationUnit? found = await units.FindByUnitNumberAsync(TestData.PropertyId, "412", Cancellation);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindByUnitNumber_AfterLoadInSameScope_ReturnsLoadedInstance()
    {
        AccommodationUnit unit = TestData.AccommodationUnit(TestData.TenantA, 1, "412");
        await SaveAsync(unit);
        await using PersistenceSession session = OpenSession();
        IAccommodationUnitRepository units = session.Get<IAccommodationUnitRepository>();
        AccommodationUnit? loaded = await units.GetByIdAsync(unit.Id, Cancellation);
        AccommodationUnit? found = await units.FindByUnitNumberAsync(TestData.PropertyId, "412", Cancellation);

        Assert.NotNull(loaded);
        Assert.Same(loaded, found);
    }

    [Fact]
    public async Task FindByCode_IgnoresCase_AndIsScopedToProperty()
    {
        Service service = TestData.Service(TestData.TenantA, 1, "housekeeping.amenities");
        await SaveAsync(service);
        await using PersistenceSession session = OpenSession();
        IServiceRepository services = session.Get<IServiceRepository>();

        Service? found = await services.FindByCodeAsync(TestData.PropertyId, "Housekeeping.Amenities", Cancellation);
        Service? otherProperty = await services.FindByCodeAsync(TestData.OtherPropertyId, "housekeeping.amenities", Cancellation);

        Assert.Equal(service.Id, found?.Id);
        Assert.Null(otherProperty);
    }

    [Fact]
    public async Task FindBySubject_MatchesKindAndIdInProperty()
    {
        OperatingHours poolHours = TestData.OperatingHours(TestData.TenantA, 1, TestData.Pool);
        await SaveAsync(poolHours, TestData.OperatingHours(TestData.TenantA, 2));
        await using PersistenceSession session = OpenSession();
        IOperatingHoursRepository hours = session.Get<IOperatingHoursRepository>();

        OperatingHours? found = await hours.FindBySubjectAsync(TestData.PropertyId, new SubjectReference(SubjectKind.Amenity, TestData.Pool.Id), Cancellation);
        OperatingHours? otherKind = await hours.FindBySubjectAsync(TestData.PropertyId, new SubjectReference(SubjectKind.Restaurant, TestData.Pool.Id), Cancellation);
        OperatingHours? otherProperty = await hours.FindBySubjectAsync(TestData.OtherPropertyId, TestData.Pool, Cancellation);

        Assert.Equal(poolHours.Id, found?.Id);
        Assert.Null(otherKind);
        Assert.Null(otherProperty);
    }

    [Fact]
    public async Task FindByClientMessageId_MatchesWithinConversationOnly()
    {
        Conversation conversation = TestData.Conversation(TestData.TenantA, 1);
        Conversation otherConversation = TestData.Conversation(TestData.TenantA, 2);
        GuestMessage message = TestData.GuestMessage(TestData.TenantA, 1, conversation, "wa-1");
        await SaveAsync(message);
        await using PersistenceSession session = OpenSession();
        IGuestMessageRepository messages = session.Get<IGuestMessageRepository>();

        GuestMessage? found = await messages.FindByClientMessageIdAsync(conversation.Id, "wa-1", Cancellation);
        GuestMessage? otherConversationMessage = await messages.FindByClientMessageIdAsync(otherConversation.Id, "wa-1", Cancellation);

        Assert.Equal(message.Id, found?.Id);
        Assert.Null(otherConversationMessage);
    }

    [Fact]
    public async Task Triage_IsSavedOnTheMessage_AndRaisesTriaged()
    {
        GuestMessage message = TestData.GuestMessage(TestData.TenantA, 1, TestData.Conversation(TestData.TenantA, 1));
        await SaveAsync(message);

        await using (PersistenceSession session = OpenSession())
        {
            GuestMessage loaded = (await session.Get<IGuestMessageRepository>().GetByIdAsync(message.Id, Cancellation))!;
            Assert.True(loaded.Triage(MessageIntent.ServiceRequest, TestData.Now).IsSuccess);
            Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);
            Assert.True(commit.IsSuccess, commit.IsFailure ? commit.Error.Message : null);
            Assert.Contains(commit.Value.Events, committed => committed.Event is GuestMessageTriaged);
        }

        await using PersistenceSession reread = OpenSession();
        Assert.Equal(MessageIntent.ServiceRequest, (await reread.Get<IGuestMessageRepository>().GetByIdAsync(message.Id, Cancellation))?.Intent);
    }

    [Fact]
    public async Task ListRecent_ReturnsLatestMessagesOldestFirst()
    {
        Conversation conversation = TestData.Conversation(TestData.TenantA, 1);
        GuestMessage[] sent = [.. Enumerable.Range(1, 5).Select(seed => TestData.GuestMessage(TestData.TenantA, seed, conversation, sentAt: TestData.Now.AddMinutes(seed)))];
        await SaveAsync([.. sent.Reverse(), TestData.GuestMessage(TestData.TenantA, 6, TestData.Conversation(TestData.TenantA, 2), sentAt: TestData.Now.AddHours(1))]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<GuestMessage> recent = await session.Get<IGuestMessageRepository>().ListRecentAsync(conversation.Id, 3, Cancellation);

        Assert.Equal([sent[2].Id, sent[3].Id, sent[4].Id], recent.Select(message => message.Id));
    }

    [Fact]
    public async Task FindInHouse_ReturnsOnlyCheckedInStayOfThatGuest()
    {
        Guest guest = TestData.Guest(TestData.TenantA, 1);
        StayContext reserved = TestData.StayContext(TestData.TenantA, 1, guest);
        StayContext inHouse = TestData.StayContext(TestData.TenantA, 2, guest);
        inHouse.CheckIn(null, TestData.Now);
        StayContext otherGuest = TestData.StayContext(TestData.TenantA, 3);
        otherGuest.CheckIn(null, TestData.Now);
        await SaveAsync(reserved, inHouse, otherGuest);
        await using PersistenceSession session = OpenSession();

        StayContext? found = await session.Get<IStayContextRepository>().FindInHouseAsync(TestData.PropertyId, guest.Id, Cancellation);

        Assert.Equal(inHouse.Id, found?.Id);
    }

    [Fact]
    public async Task FindInHouseInUnit_ReturnsOnlyTheCheckedInStayInThatUnitAndTenant()
    {
        AccommodationUnitId room415 = new(TestData.Id(17998));
        StayContext reserved = TestData.StayContext(TestData.TenantA, 1);
        StayContext inHouse = TestData.StayContext(TestData.TenantA, 2);
        inHouse.CheckIn(null, TestData.Now);
        StayContext otherUnit = TestData.StayContext(TestData.TenantA, 3);
        otherUnit.CheckIn(room415, TestData.Now);
        StayContext otherProperty = TestData.StayContext(TestData.TenantA, 4, propertyId: TestData.OtherPropertyId);
        otherProperty.CheckIn(null, TestData.Now);
        await SaveAsync(reserved, inHouse, otherUnit, otherProperty);
        await using PersistenceSession session = OpenSession();
        await using PersistenceSession otherTenant = OpenSession(TestData.TenantB);

        StayContext? found = await session.Get<IStayContextRepository>().FindInHouseInUnitAsync(TestData.PropertyId, TestData.Room412, Cancellation);
        StayContext? fromOtherTenant = await otherTenant.Get<IStayContextRepository>().FindInHouseInUnitAsync(TestData.PropertyId, TestData.Room412, Cancellation);
        StayContext? inEmptyUnit = await session.Get<IStayContextRepository>().FindInHouseInUnitAsync(TestData.PropertyId, new AccommodationUnitId(TestData.Id(17997)), Cancellation);

        Assert.Equal(inHouse.Id, found?.Id);
        Assert.Null(fromOtherTenant);
        Assert.Null(inEmptyUnit);
    }

    [Fact]
    public async Task FindBySourceMessage_ReturnsRequestCreatedFromThatMessage()
    {
        GuestMessageId source = new(TestData.Id(1));
        ServiceRequest request = TestData.ServiceRequest(TestData.TenantA, 1, source);
        await SaveAsync(request, TestData.ServiceRequest(TestData.TenantA, 2, new GuestMessageId(TestData.Id(2))));
        await using PersistenceSession session = OpenSession();
        IServiceRequestRepository requests = session.Get<IServiceRequestRepository>();

        ServiceRequest? found = await requests.FindBySourceMessageAsync(source, Cancellation);
        ServiceRequest? unknown = await requests.FindBySourceMessageAsync(new GuestMessageId(TestData.Id(3)), Cancellation);

        Assert.Equal(request.Id, found?.Id);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task ListActiveForStay_ReturnsUnresolvedRequestsNewestFirst()
    {
        StayContextId stay = new(TestData.Id(1));
        ServiceRequest oldest = TestData.ServiceRequest(TestData.TenantA, 1, stayContextId: stay, createdAt: TestData.Now);
        ServiceRequest cancelled = TestData.ServiceRequest(TestData.TenantA, 2, stayContextId: stay, createdAt: TestData.Now.AddMinutes(1));
        cancelled.Cancel(TestData.Now.AddMinutes(2));
        ServiceRequest newest = TestData.ServiceRequest(TestData.TenantA, 3, stayContextId: stay, createdAt: TestData.Now.AddMinutes(3));
        newest.RecordException("Out of towels", TestData.Now.AddMinutes(4));
        ServiceRequest otherStay = TestData.ServiceRequest(TestData.TenantA, 4, stayContextId: new StayContextId(TestData.Id(2)), createdAt: TestData.Now);
        await SaveAsync(oldest, cancelled, newest, otherStay);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<ServiceRequest> active = await session.Get<IServiceRequestRepository>().ListActiveForStayAsync(stay, Cancellation);

        Assert.Equal([newest.Id, oldest.Id], active.Select(request => request.Id));
    }

    [Fact]
    public async Task ListForServiceRequest_ReturnsItsActionsOldestFirst()
    {
        ServiceRequestId requestId = new(TestData.Id(1));
        HumanAction later = TestData.HumanAction(TestData.TenantA, 1, requestId, TestData.Now.AddMinutes(5));
        HumanAction earlier = TestData.HumanAction(TestData.TenantA, 2, requestId, TestData.Now);
        HumanAction otherRequest = TestData.HumanAction(TestData.TenantA, 3, new ServiceRequestId(TestData.Id(2)));
        await SaveAsync(later, earlier, otherRequest);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<HumanAction> actions = await session.Get<IHumanActionRepository>().ListForServiceRequestAsync(requestId, Cancellation);

        Assert.Equal([earlier.Id, later.Id], actions.Select(action => action.Id));
    }

    [Fact]
    public async Task FindSucceeded_MatchesSucceededInvocationOfThatCapabilityAndKeyOnly()
    {
        CapabilityInvocation succeeded = TestData.CapabilityInvocation(TestData.TenantA, 1, "key-1", succeeded: true);
        await SaveAsync(
            succeeded,
            TestData.CapabilityInvocation(TestData.TenantA, 2, "key-2"),
            TestData.CapabilityInvocation(TestData.TenantA, 3, "key-3", succeeded: true, capabilityName: "HumanAction.Complete"),
            TestData.CapabilityInvocation(TestData.TenantB, 4, "key-4", succeeded: true));
        await using PersistenceSession session = OpenSession();
        ICapabilityInvocationRepository invocations = session.Get<ICapabilityInvocationRepository>();

        CapabilityInvocation? found = await invocations.FindSucceededAsync("GuestService.CreateRequest", "key-1", Cancellation);

        Assert.Equal((succeeded.Id, succeeded.Output), (found?.Id, found?.Output));
        Assert.Null(await invocations.FindSucceededAsync("GuestService.CreateRequest", "KEY-1", Cancellation));
        Assert.Null(await invocations.FindSucceededAsync("GuestService.CreateRequest", "key-2", Cancellation));
        Assert.Null(await invocations.FindSucceededAsync("GuestService.CreateRequest", "key-3", Cancellation));
        Assert.Null(await invocations.FindSucceededAsync("GuestService.CreateRequest", "key-4", Cancellation));
    }

    [Fact]
    public async Task ListOrganizations_ReturnsTheTenantsOrganizations()
    {
        Organization[] mine = [TestData.Organization(TestData.TenantA, 1), TestData.Organization(TestData.TenantA, 2)];
        await SaveAsync([.. mine, TestData.Organization(TestData.TenantB, 3)]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<Organization> found = await session.Get<IOrganizationRepository>().ListAsync(Cancellation);

        AssertSameIds(mine.Select(o => o.Id), found.Select(o => o.Id));
    }

    [Fact]
    public async Task ListProperties_AllOrOneOrganizations_InTheTenant()
    {
        OrganizationId otherOrganization = new(TestData.Id(1999));
        Property grand = TestData.Property(TestData.TenantA, 1);
        Property harbour = TestData.Property(TestData.TenantA, 2, otherOrganization);
        await SaveAsync(grand, harbour, TestData.Property(TestData.TenantB, 3));
        await using PersistenceSession session = OpenSession();
        IPropertyRepository properties = session.Get<IPropertyRepository>();

        IReadOnlyList<Property> all = await properties.ListAsync(null, Cancellation);
        IReadOnlyList<Property> ofOrganization = await properties.ListAsync(otherOrganization, Cancellation);

        AssertSameIds([grand.Id, harbour.Id], all.Select(p => p.Id));
        AssertSameIds([harbour.Id], ofOrganization.Select(p => p.Id));
    }

    [Fact]
    public async Task ListAmenitiesForProperty_ReturnsThatPropertysAmenitiesInTheTenant()
    {
        Amenity[] mine = [TestData.Amenity(TestData.TenantA, 1), TestData.Amenity(TestData.TenantA, 2)];
        await SaveAsync([.. mine, TestData.Amenity(TestData.TenantA, 3, TestData.OtherPropertyId), TestData.Amenity(TestData.TenantB, 4)]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<Amenity> found = await session.Get<IAmenityRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds(mine.Select(a => a.Id), found.Select(a => a.Id));
    }

    [Fact]
    public async Task ListSpacesForProperty_ReturnsThatPropertysSpacesInTheTenant()
    {
        Space[] mine = [TestData.Space(TestData.TenantA, 1), TestData.Space(TestData.TenantA, 2)];
        await SaveAsync([.. mine, TestData.Space(TestData.TenantA, 3, TestData.OtherPropertyId), TestData.Space(TestData.TenantB, 4)]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<Space> found = await session.Get<ISpaceRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds(mine.Select(s => s.Id), found.Select(s => s.Id));
    }

    [Fact]
    public async Task ListOperatingHoursForProperty_ReturnsThatPropertysHoursInTheTenant()
    {
        OperatingHours[] mine = [TestData.OperatingHours(TestData.TenantA, 1), TestData.OperatingHours(TestData.TenantA, 2)];
        await SaveAsync([.. mine, TestData.OperatingHours(TestData.TenantA, 3, propertyId: TestData.OtherPropertyId), TestData.OperatingHours(TestData.TenantB, 4)]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<OperatingHours> found = await session.Get<IOperatingHoursRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds(mine.Select(h => h.Id), found.Select(h => h.Id));
    }

    [Fact]
    public async Task ListRestaurantsForProperty_ReturnsThatPropertysRestaurantsInTheTenant()
    {
        Restaurant[] mine = [TestData.Restaurant(TestData.TenantA, 1), TestData.Restaurant(TestData.TenantA, 2)];
        await SaveAsync([.. mine, TestData.Restaurant(TestData.TenantA, 3, TestData.OtherPropertyId), TestData.Restaurant(TestData.TenantB, 4)]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<Restaurant> found = await session.Get<IRestaurantRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds(mine.Select(r => r.Id), found.Select(r => r.Id));
    }

    [Fact]
    public async Task ListPropertyEventsForProperty_ReturnsPastAndCancelledEventsOfThatPropertyInTheTenant()
    {
        PropertyEvent past = TestData.PropertyEvent(TestData.TenantA, 1, localStart: new DateTime(2020, 1, 1, 19, 0, 0));
        PropertyEvent cancelled = TestData.PropertyEvent(TestData.TenantA, 2);
        Assert.True(cancelled.Cancel(TestData.Now).IsSuccess);
        PropertyEvent[] mine = [past, cancelled, TestData.PropertyEvent(TestData.TenantA, 3)];
        await SaveAsync([.. mine, TestData.PropertyEvent(TestData.TenantA, 4, TestData.OtherPropertyId), TestData.PropertyEvent(TestData.TenantB, 5)]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<PropertyEvent> found = await session.Get<IPropertyEventRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds(mine.Select(e => e.Id), found.Select(e => e.Id));
    }

    [Fact]
    public async Task ListServicesForProperty_ReturnsThatPropertysServicesInTheTenant()
    {
        Service[] mine = [TestData.Service(TestData.TenantA, 1, "a.one"), TestData.Service(TestData.TenantA, 2, "a.two")];
        await SaveAsync([.. mine, TestData.Service(TestData.TenantA, 3, "a.three", TestData.OtherPropertyId), TestData.Service(TestData.TenantB, 4, "b.four")]);
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<Service> found = await session.Get<IServiceRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds(mine.Select(s => s.Id), found.Select(s => s.Id));
    }

    [Fact]
    public async Task ListMenus_ByRestaurantAndByProperty_ReturnOnlyMatchingMenusInTheTenant()
    {
        Restaurant saffron = TestData.Restaurant(TestData.TenantA, 1);
        Restaurant elsewhere = TestData.Restaurant(TestData.TenantA, 2, TestData.OtherPropertyId);
        Menu dinner = TestData.Menu(TestData.TenantA, 1, saffron);
        Menu lunch = TestData.Menu(TestData.TenantA, 2, saffron);
        Menu bar = TestData.Menu(TestData.TenantA, 3);
        await SaveAsync(saffron, elsewhere, dinner, lunch, bar, TestData.Menu(TestData.TenantA, 4, elsewhere), TestData.Menu(TestData.TenantB, 5));
        await using PersistenceSession session = OpenSession();
        IMenuRepository menus = session.Get<IMenuRepository>();

        IReadOnlyList<Menu> forRestaurant = await menus.ListForRestaurantAsync(saffron.Id, Cancellation);
        IReadOnlyList<Menu> forProperty = await menus.ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds([dinner.Id, lunch.Id], forRestaurant.Select(m => m.Id));
        AssertSameIds([dinner.Id, lunch.Id, bar.Id], forProperty.Select(m => m.Id));
    }

    [Fact]
    public async Task ListActivePolicies_ReturnsOnlyActivePoliciesOfThatProperty()
    {
        Policy active = Activated(TestData.Policy(TestData.TenantA, 1));
        Policy retired = Activated(TestData.Policy(TestData.TenantA, 2));
        Assert.True(retired.Retire(TestData.Now).IsSuccess);
        await SaveAsync(
            active,
            retired,
            TestData.Policy(TestData.TenantA, 3),
            Activated(TestData.Policy(TestData.TenantA, 4, TestData.OtherPropertyId)),
            Activated(TestData.Policy(TestData.TenantB, 5)));
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<Policy> found = await session.Get<IPolicyRepository>().ListActiveForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds([active.Id], found.Select(p => p.Id));
    }

    [Fact]
    public async Task ListPoliciesForProperty_ReturnsEveryStatusOfThatPropertyInTheTenant()
    {
        Policy draft = TestData.Policy(TestData.TenantA, 1);
        Policy active = Activated(TestData.Policy(TestData.TenantA, 2));
        Policy retired = Activated(TestData.Policy(TestData.TenantA, 3));
        Assert.True(retired.Retire(TestData.Now).IsSuccess);
        await SaveAsync(draft, active, retired, TestData.Policy(TestData.TenantA, 4, TestData.OtherPropertyId), TestData.Policy(TestData.TenantB, 5));
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<Policy> found = await session.Get<IPolicyRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds([draft.Id, active.Id, retired.Id], found.Select(p => p.Id));
    }

    [Fact]
    public async Task ListPublishedContent_ReturnsOnlyPublishedItemsOfThatProperty()
    {
        ContentItem published = Published(TestData.ContentItem(TestData.TenantA, 1));
        ContentItem archived = Published(TestData.ContentItem(TestData.TenantA, 2));
        Assert.True(archived.Archive(TestData.Now).IsSuccess);
        await SaveAsync(
            published,
            archived,
            TestData.ContentItem(TestData.TenantA, 3),
            Published(TestData.ContentItem(TestData.TenantA, 4, TestData.OtherPropertyId)),
            Published(TestData.ContentItem(TestData.TenantB, 5)));
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<ContentItem> found = await session.Get<IContentItemRepository>().ListPublishedForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds([published.Id], found.Select(c => c.Id));
    }

    [Fact]
    public async Task ListContentForProperty_ReturnsEveryStatusOfThatPropertyInTheTenant()
    {
        ContentItem draft = TestData.ContentItem(TestData.TenantA, 1);
        ContentItem published = Published(TestData.ContentItem(TestData.TenantA, 2));
        ContentItem archived = Published(TestData.ContentItem(TestData.TenantA, 3));
        Assert.True(archived.Archive(TestData.Now).IsSuccess);
        await SaveAsync(draft, published, archived, TestData.ContentItem(TestData.TenantA, 4, TestData.OtherPropertyId), TestData.ContentItem(TestData.TenantB, 5));
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<ContentItem> found = await session.Get<IContentItemRepository>().ListForPropertyAsync(TestData.PropertyId, Cancellation);

        AssertSameIds([draft.Id, published.Id, archived.Id], found.Select(c => c.Id));
    }

    [Fact]
    public async Task ListScheduledEndingAfter_ReturnsOngoingAndFutureScheduledEventsOfThatProperty()
    {
        DateTime localNow = new(2026, 10, 26, 21, 0, 0);
        PropertyEvent ongoing = TestData.PropertyEvent(TestData.TenantA, 1, localStart: localNow.AddHours(-1));
        PropertyEvent future = TestData.PropertyEvent(TestData.TenantA, 2, localStart: localNow.AddDays(1));
        PropertyEvent cancelled = TestData.PropertyEvent(TestData.TenantA, 3, localStart: localNow.AddDays(1));
        Assert.True(cancelled.Cancel(TestData.Now).IsSuccess);
        await SaveAsync(
            ongoing,
            future,
            cancelled,
            TestData.PropertyEvent(TestData.TenantA, 4, localStart: localNow.AddHours(-2)),
            TestData.PropertyEvent(TestData.TenantA, 5, TestData.OtherPropertyId, localNow.AddDays(1)),
            TestData.PropertyEvent(TestData.TenantB, 6, localStart: localNow.AddDays(1)));
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<PropertyEvent> found = await session.Get<IPropertyEventRepository>().ListScheduledEndingAfterAsync(TestData.PropertyId, localNow, Cancellation);

        AssertSameIds([ongoing.Id, future.Id], found.Select(e => e.Id));
    }

    [Fact]
    public async Task FindIntegrationConfigurationForProperty_MatchesPropertyAndKindInTheTenant()
    {
        IntegrationConfiguration pms = TestData.IntegrationConfiguration(TestData.TenantA, 1, ConnectorKind.Pms);
        await SaveAsync(
            pms,
            TestData.IntegrationConfiguration(TestData.TenantA, 2, ConnectorKind.Messaging),
            TestData.IntegrationConfiguration(TestData.TenantA, 3, ConnectorKind.Pms, TestData.OtherPropertyId),
            TestData.IntegrationConfiguration(TestData.TenantB, 4, ConnectorKind.Cms));
        await using PersistenceSession session = OpenSession();
        IIntegrationConfigurationRepository configurations = session.Get<IIntegrationConfigurationRepository>();

        IntegrationConfiguration? found = await configurations.FindForPropertyAsync(TestData.PropertyId, ConnectorKind.Pms, Cancellation);
        IntegrationConfiguration? otherTenantsKind = await configurations.FindForPropertyAsync(TestData.PropertyId, ConnectorKind.Cms, Cancellation);
        IReadOnlyList<IntegrationConfiguration> all = await configurations.ListForPropertyAsync(TestData.PropertyId, Cancellation);

        Assert.Equal(pms.Id, found?.Id);
        Assert.Null(otherTenantsKind);
        AssertSameIds([pms.Id, new IntegrationConfigurationId(TestData.Id(23002))], all.Select(c => c.Id));
    }

    [Fact]
    public async Task FindExternalReference_MatchesExactlyInTheTenant()
    {
        ExternalReference reference = TestData.ExternalReference(TestData.TenantA, 1, "RES-0412");
        await SaveAsync(reference, TestData.ExternalReference(TestData.TenantB, 2, "RES-0999"));
        await using PersistenceSession session = OpenSession();
        IExternalReferenceRepository references = session.Get<IExternalReferenceRepository>();

        ExternalReference? found = await references.FindAsync(TestData.PropertyId, "MockPms", "StayContext", "RES-0412", Cancellation);
        ExternalReference? otherCase = await references.FindAsync(TestData.PropertyId, "MockPms", "StayContext", "res-0412", Cancellation);
        ExternalReference? otherProvider = await references.FindAsync(TestData.PropertyId, "OperaCloud", "StayContext", "RES-0412", Cancellation);
        ExternalReference? otherType = await references.FindAsync(TestData.PropertyId, "MockPms", "Guest", "RES-0412", Cancellation);
        ExternalReference? otherProperty = await references.FindAsync(TestData.OtherPropertyId, "MockPms", "StayContext", "RES-0412", Cancellation);
        ExternalReference? otherTenant = await references.FindAsync(TestData.PropertyId, "MockPms", "StayContext", "RES-0999", Cancellation);

        Assert.Equal((reference.Id, reference.CanonicalId), (found?.Id, found?.CanonicalId));
        Assert.All([otherCase, otherProvider, otherType, otherProperty, otherTenant], Assert.Null);
    }

    [Fact]
    public async Task ListExternalReferencesForCanonical_ReturnsEveryProvidersIdsForTheRecord()
    {
        Guid stay = TestData.Id(25999);
        ExternalReference mock = TestData.ExternalReference(TestData.TenantA, 1, "RES-0412", stay);
        ExternalReference opera = TestData.ExternalReference(TestData.TenantA, 2, "8812734", stay, "OperaCloud");
        await SaveAsync(mock, opera, TestData.ExternalReference(TestData.TenantA, 3, "RES-0413"), TestData.ExternalReference(TestData.TenantB, 4, "RES-0500", stay));
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<ExternalReference> found = await session.Get<IExternalReferenceRepository>().ListForCanonicalAsync(TestData.PropertyId, "StayContext", stay, Cancellation);

        AssertSameIds([mock.Id, opera.Id], found.Select(r => r.Id));
    }

    [Fact]
    public async Task ListTraces_NewestFirst_ThenByIdAtTheSameTime_AndPagesFromTheLastOneSeen()
    {
        DateTimeOffset at = TestData.Now;
        AITrace[] traces =
        [
            TestData.AITrace(TestData.TenantA, 1, at),
            TestData.AITrace(TestData.TenantA, 2, at.AddSeconds(1)),
            TestData.AITrace(TestData.TenantA, 3, at.AddSeconds(1)),
            TestData.AITrace(TestData.TenantA, 4, at.AddSeconds(1)),
            TestData.AITrace(TestData.TenantA, 5, at.AddSeconds(2)),

            // The highest ID but the earliest completion: time decides, not the ID.
            TestData.AITrace(TestData.TenantA, 6, at.AddSeconds(-1)),
        ];
        await SaveAsync([.. traces, TestData.AITrace(TestData.TenantB, 7, at.AddSeconds(3))]);
        await using PersistenceSession session = OpenSession();
        IAITraceRepository repository = session.Get<IAITraceRepository>();
        List<AITraceId> read = [];
        AITraceCursor? after = null;

        for (int page = 0; page < 4; page++)
        {
            IReadOnlyList<AITrace> found = await repository.ListAsync(new AITraceQuery(Limit: 2, After: after), Cancellation);
            read.AddRange(found.Select(trace => trace.Id));
            after = found.Count == 0 ? after : new AITraceCursor(found[^1].CompletedAt, found[^1].Id);
        }

        Assert.Equal([traces[4].Id, traces[3].Id, traces[2].Id, traces[1].Id, traces[0].Id, traces[5].Id], read);
    }

    [Fact]
    public async Task ListTraces_FiltersByPropertyFlowOutcomeAndCompletionTime()
    {
        DateTimeOffset at = TestData.Now;
        Guid flow = TestData.Id(24999);
        AITrace match = TestData.AITrace(TestData.TenantA, 1, at, correlationId: flow, outcome: AITraceOutcome.NoAuthoritativeSource);
        await SaveAsync(
            match,
            TestData.AITrace(TestData.TenantA, 2, at, TestData.OtherPropertyId, flow, AITraceOutcome.NoAuthoritativeSource),
            TestData.AITrace(TestData.TenantA, 3, at, correlationId: flow),
            TestData.AITrace(TestData.TenantA, 4, at, outcome: AITraceOutcome.NoAuthoritativeSource),
            TestData.AITrace(TestData.TenantA, 5, at.AddMinutes(-1), correlationId: flow, outcome: AITraceOutcome.NoAuthoritativeSource),
            TestData.AITrace(TestData.TenantA, 6, at.AddMinutes(1), correlationId: flow, outcome: AITraceOutcome.NoAuthoritativeSource),
            TestData.AITrace(TestData.TenantB, 7, at, correlationId: flow, outcome: AITraceOutcome.NoAuthoritativeSource));
        await using PersistenceSession session = OpenSession();

        IReadOnlyList<AITrace> found = await session.Get<IAITraceRepository>().ListAsync(
            new AITraceQuery(
                PropertyId: TestData.PropertyId,
                CorrelationId: flow,
                Outcome: AITraceOutcome.NoAuthoritativeSource,
                CompletedFrom: at,
                CompletedBefore: at.AddMinutes(1)),
            Cancellation);

        Assert.Equal([match.Id], found.Select(trace => trace.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(AITraceQuery.MaxLimit + 1)]
    public async Task ListTraces_ALimitOutOfRange_Throws(int limit)
    {
        await using PersistenceSession session = OpenSession();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.Get<IAITraceRepository>().ListAsync(new AITraceQuery(Limit: limit), Cancellation));
    }

    private static Policy Activated(Policy policy)
    {
        Assert.True(policy.Activate(TestData.Now).IsSuccess);

        return policy;
    }

    private static ContentItem Published(ContentItem item)
    {
        Assert.True(item.Publish(TestData.Now).IsSuccess);

        return item;
    }

    private static void AssertSameIds<TId>(IEnumerable<TId> expected, IEnumerable<TId> actual)
        where TId : struct, HospitalityOS.SharedKernel.Ids.ITypedId =>
        Assert.Equal(expected.Select(id => id.Value).Order(), actual.Select(id => id.Value).Order());
}
