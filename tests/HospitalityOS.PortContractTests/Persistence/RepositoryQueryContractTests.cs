using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.Domain.Operations.HumanActions;
using HospitalityOS.Domain.Properties.AccommodationUnits;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.SharedKernel.Ids;
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
}
