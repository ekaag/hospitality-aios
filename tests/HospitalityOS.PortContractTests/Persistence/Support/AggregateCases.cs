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
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Ids;
using Xunit;

namespace HospitalityOS.PortContractTests.Persistence.Support;

/// <summary>
/// Every aggregate with a repository, so the generic contract covers all of them; a new aggregate is added here.
/// </summary>
public static class AggregateCases
{
    private static readonly AggregateCase[] all =
    [
        new AggregateCase<Organization, OrganizationId, IOrganizationRepository>(
            TestData.Organization, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Rename("Aurora Hotels Group", TestData.Now)),
        new AggregateCase<Property, PropertyId, IPropertyRepository>(
            TestData.Property, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Update(a.Details with { ContactPhone = "+44 20 7946 0000" }, TestData.Now)),
        new AggregateCase<Space, SpaceId, ISpaceRepository>(
            (tenant, seed) => TestData.Space(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Update(a.Details with { Directions = "Take the lift" }, TestData.Now)),
        new AggregateCase<AccommodationType, AccommodationTypeId, IAccommodationTypeRepository>(
            (tenant, seed) => TestData.AccommodationType(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Update(a.Details with { MaxOccupancy = 3 }, TestData.Now)),
        new AggregateCase<AccommodationUnit, AccommodationUnitId, IAccommodationUnitRepository>(
            (tenant, seed) => TestData.AccommodationUnit(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Renumber(a.UnitNumber + "A", TestData.Now)),
        new AggregateCase<OperatingHours, OperatingHoursId, IOperatingHoursRepository>(
            (tenant, seed) => TestData.OperatingHours(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Update(TestData.Weekly(DayOfWeek.Monday, DayOfWeek.Tuesday), TestData.Now)),
        new AggregateCase<Amenity, AmenityId, IAmenityRepository>(
            TestData.Amenity, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Update(a.Details with { Description = "Heated indoor pool" }, null, TestData.Now)),
        new AggregateCase<Policy, PolicyId, IPolicyRepository>(
            TestData.Policy, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Activate(TestData.Now)),
        new AggregateCase<ContentItem, ContentItemId, IContentItemRepository>(
            TestData.ContentItem, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Publish(TestData.Now)),
        new AggregateCase<Restaurant, RestaurantId, IRestaurantRepository>(
            TestData.Restaurant, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Update(a.Details with { Cuisine = "Indian" }, TestData.Now)),
        new AggregateCase<Menu, MenuId, IMenuRepository>(
            TestData.Menu, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.SetItemAvailability(TestData.PaneerTikka, false, TestData.Now)),
        new AggregateCase<PropertyEvent, PropertyEventId, IPropertyEventRepository>(
            TestData.PropertyEvent, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Cancel(TestData.Now)),
        new AggregateCase<Service, ServiceId, IServiceRepository>(
            (tenant, seed) => TestData.Service(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Update(a.Details with { Name = "Amenities" }, TestData.Now)),
        new AggregateCase<ServiceRequest, ServiceRequestId, IServiceRequestRepository>(
            (tenant, seed) => TestData.ServiceRequest(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.MarkInProgress(TestData.Now)),
        new AggregateCase<HumanAction, HumanActionId, IHumanActionRepository>(
            (tenant, seed) => TestData.HumanAction(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Assign(TestData.Maria, "HumanActionRouter", TestData.Now)),
        new AggregateCase<Guest, GuestId, IGuestRepository>(
            TestData.Guest, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.UpdateProfile(a.Profile with { PreferredLanguage = "fr" }, TestData.Now)),
        new AggregateCase<StayContext, StayContextId, IStayContextRepository>(
            (tenant, seed) => TestData.StayContext(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.CheckIn(null, TestData.Now)),
        new AggregateCase<Conversation, ConversationId, IConversationRepository>(
            TestData.Conversation, id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            a => a.Close(TestData.Now)),
        new AggregateCase<GuestMessage, GuestMessageId, IGuestMessageRepository>(
            (tenant, seed) => TestData.GuestMessage(tenant, seed), id => new(id), (repository, a, ct) => repository.AddAsync(a, ct), (repository, id, ct) => repository.GetByIdAsync(id, ct),
            change: null),
    ];

    public static TheoryData<string> Names => new(all.Select(aggregateCase => aggregateCase.Name));

    internal static AggregateCase Get(string name) => all.Single(aggregateCase => aggregateCase.Name == name);
}
