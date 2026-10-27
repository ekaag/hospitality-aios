using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Capabilities.Reading;

/// <summary>
/// The Aurora Grand, London, as the read capabilities see it at <see cref="Now"/>: Monday 26 October 2026, 18:30 local
/// (GMT, so UTC). Pool and Saffron have hours, the gym and Harbour Grill do not; policies, content, and events cover
/// every status a read must include or leave out.
/// </summary>
internal sealed class AuroraGrand
{
    public static DateTimeOffset Now { get; } = new(2026, 10, 26, 18, 30, 0, TimeSpan.Zero);
    public static DateTime LocalNow { get; } = new(2026, 10, 26, 18, 30, 0);
    public static DateOnly Christmas { get; } = new(2026, 12, 25);
    public static DateOnly MaintenanceDay { get; } = new(2026, 11, 2);

    private readonly List<IAggregateRoot> records = [];

    public AuroraGrand(TenantId tenantId)
    {
        TenantId = tenantId;
        Property = Create(Property.Create(PropertyId, tenantId, new OrganizationId(Guid.CreateVersion7()), new PropertyDetails(
            "Aurora Grand",
            "Europe/London",
            new PostalAddress("1 Strand", null, "London", null, "WC2N 5HR", "GB"),
            "+44 20 7946 0000"), Now));
        Pool = Create(Amenity.Create(new AmenityId(Guid.CreateVersion7()), tenantId, PropertyId, new AmenityDetails("Pool", AmenityCategory.Pool, "Indoor, heated"), null, Now));
        Gym = Create(Amenity.Create(new AmenityId(Guid.CreateVersion7()), tenantId, PropertyId, new AmenityDetails("Gym", AmenityCategory.Fitness), null, Now));
        PoolHours = Create(OperatingHours.Create(
            new OperatingHoursId(Guid.CreateVersion7()),
            tenantId,
            PropertyId,
            new SubjectReference(SubjectKind.Amenity, Pool.Id.Value),
            new OperatingHoursSchedule(
                Daily(new TimeInterval(new TimeOnly(7, 0), new TimeOnly(22, 0))),
                [new DateOverride(Christmas, [new TimeInterval(new TimeOnly(10, 0), new TimeOnly(14, 0))], "Christmas hours")],
                [new TemporaryClosure(MaintenanceDay, MaintenanceDay.AddDays(1), "Maintenance")]),
            Now));
        Saffron = Create(Restaurant.Create(new RestaurantId(Guid.CreateVersion7()), tenantId, PropertyId, new RestaurantDetails("Saffron", "Indian", "Modern Indian dining"), Now));
        HarbourGrill = Create(Restaurant.Create(new RestaurantId(Guid.CreateVersion7()), tenantId, PropertyId, new RestaurantDetails("Harbour Grill", "Seafood"), Now));
        SaffronHours = Create(OperatingHours.Create(
            new OperatingHoursId(Guid.CreateVersion7()),
            tenantId,
            PropertyId,
            new SubjectReference(SubjectKind.Restaurant, Saffron.Id.Value),
            new OperatingHoursSchedule(Daily(new TimeInterval(new TimeOnly(18, 0), new TimeOnly(23, 0))), [], []),
            Now));
        SaffronDinner = Create(Menu.Create(new MenuId(Guid.CreateVersion7()), Saffron, new MenuDetails("Dinner", [
            new MenuItem(PaneerTikka, "Paneer tikka", Price: new Money(14.50m, "GBP"), Category: "Starters", Dietary: [DietaryTag.Vegetarian]),
            new MenuItem(new MenuItemId(Guid.CreateVersion7()), "Lamb rogan josh", Price: new Money(22m, "GBP"), Category: "Mains"),
            new MenuItem(new MenuItemId(Guid.CreateVersion7()), "Tarka dal", Category: "Mains", Dietary: [DietaryTag.Vegetarian, DietaryTag.Vegan], IsAvailable: false),
        ]), Now));
        Create(Menu.Create(new MenuId(Guid.CreateVersion7()), HarbourGrill, new MenuDetails("Lunch", [new MenuItem(new MenuItemId(Guid.CreateVersion7()), "Fish and chips")]), Now));

        GymChildren = Active(Policy.Create(new PolicyId(Guid.CreateVersion7()), tenantId, PropertyId, new PolicyDetails(
            PolicyType.Children, "Children in the gym", "Guests under 16 must be accompanied by an adult.", AppliesTo: new SubjectReference(SubjectKind.Amenity, Gym.Id.Value)), Now));
        Pets = Active(Policy.Create(new PolicyId(Guid.CreateVersion7()), tenantId, PropertyId, new PolicyDetails(PolicyType.Pets, "Pets", "Dogs under 15 kg are welcome."), Now));
        Create(Policy.Create(new PolicyId(Guid.CreateVersion7()), tenantId, PropertyId, new PolicyDetails(PolicyType.Smoking, "Smoking (draft)", "Not yet approved."), Now));
        Policy retired = Active(Policy.Create(new PolicyId(Guid.CreateVersion7()), tenantId, PropertyId, new PolicyDetails(PolicyType.Noise, "Old quiet hours", "Retired."), Now));
        Assert.True(retired.Retire(Now).IsSuccess);
        Active(Policy.Create(new PolicyId(Guid.CreateVersion7()), tenantId, PropertyId, new PolicyDetails(
            PolicyType.Cancellation, "2027 cancellation terms", "Not in effect yet.", EffectiveFrom: new DateOnly(2027, 1, 1)), Now));

        WiFi = Published(ContentItem.Create(new ContentItemId(Guid.CreateVersion7()), tenantId, PropertyId, new ContentItemDetails(
            ContentType.Faq, "Wi-Fi", "Join the Aurora-Guest network; the password is on your key card.", Tags: ["internet"]), Now));
        Parking = Published(ContentItem.Create(new ContentItemId(Guid.CreateVersion7()), tenantId, PropertyId, new ContentItemDetails(
            ContentType.Directions, "Parking", "The car park entrance is on Savoy Street; the network of signs leads to valet parking."), Now));
        Create(ContentItem.Create(new ContentItemId(Guid.CreateVersion7()), tenantId, PropertyId, new ContentItemDetails(ContentType.Announcement, "Spa opening", "Wi-Fi in the spa soon."), Now));

        JazzNight = Create(PropertyEvent.Create(new PropertyEventId(Guid.CreateVersion7()), tenantId, PropertyId, new PropertyEventDetails(
            "Jazz night", PropertyEventCategory.Entertainment, EventAudience.Everyone, LocalNow.Date.AddHours(20), LocalNow.Date.AddHours(22)), Now));
        KidsClub = Create(PropertyEvent.Create(new PropertyEventId(Guid.CreateVersion7()), tenantId, PropertyId, new PropertyEventDetails(
            "Kids club", PropertyEventCategory.Family, EventAudience.Children, LocalNow.Date.AddHours(17), LocalNow.Date.AddHours(19)), Now));
        CookingClass = Create(PropertyEvent.Create(new PropertyEventId(Guid.CreateVersion7()), tenantId, PropertyId, new PropertyEventDetails(
            "Cooking class", PropertyEventCategory.FoodAndDrink, EventAudience.AdultsOnly, LocalNow.Date.AddDays(1).AddHours(15), LocalNow.Date.AddDays(1).AddHours(17),
            Price: new Money(45m, "GBP"), BookingRequired: true), Now));
        Create(PropertyEvent.Create(new PropertyEventId(Guid.CreateVersion7()), tenantId, PropertyId, new PropertyEventDetails(
            "Morning yoga", PropertyEventCategory.Wellness, EventAudience.Everyone, LocalNow.Date.AddHours(7), LocalNow.Date.AddHours(8)), Now));
        PropertyEvent cancelled = Create(PropertyEvent.Create(new PropertyEventId(Guid.CreateVersion7()), tenantId, PropertyId, new PropertyEventDetails(
            "Wine tasting", PropertyEventCategory.FoodAndDrink, EventAudience.AdultsOnly, LocalNow.Date.AddDays(1).AddHours(19), LocalNow.Date.AddDays(1).AddHours(21)), Now));
        Assert.True(cancelled.Cancel(Now).IsSuccess);
    }

    public static MenuItemId PaneerTikka { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000777"));

    public TenantId TenantId { get; }

    public PropertyId PropertyId { get; } = new(Guid.CreateVersion7());

    public Property Property { get; }

    public Amenity Pool { get; }

    public Amenity Gym { get; }

    public OperatingHours PoolHours { get; }

    public Restaurant Saffron { get; }

    public Restaurant HarbourGrill { get; }

    public OperatingHours SaffronHours { get; }

    public Menu SaffronDinner { get; }

    public Policy GymChildren { get; }

    public Policy Pets { get; }

    public ContentItem WiFi { get; }

    public ContentItem Parking { get; }

    public PropertyEvent JazzNight { get; }

    public PropertyEvent KidsClub { get; }

    public PropertyEvent CookingClass { get; }

    public async Task SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(TenantId);

        foreach (IAggregateRoot record in records)
        {
            await AddAsync(scope.ServiceProvider, record, cancellationToken);
        }

        Result<CommitReceipt> commit = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
        Assert.True(commit.IsSuccess, commit.IsFailure ? commit.Error.Message : null);
    }

    private static ValueList<DayHours> Daily(TimeInterval interval) => ValueList.From(Enum.GetValues<DayOfWeek>().Select(day => new DayHours(day, [interval])));

    private static Task AddAsync(IServiceProvider services, IAggregateRoot record, CancellationToken cancellationToken) => record switch
    {
        Property property => services.GetRequiredService<IPropertyRepository>().AddAsync(property, cancellationToken),
        Amenity amenity => services.GetRequiredService<IAmenityRepository>().AddAsync(amenity, cancellationToken),
        OperatingHours hours => services.GetRequiredService<IOperatingHoursRepository>().AddAsync(hours, cancellationToken),
        Restaurant restaurant => services.GetRequiredService<IRestaurantRepository>().AddAsync(restaurant, cancellationToken),
        Menu menu => services.GetRequiredService<IMenuRepository>().AddAsync(menu, cancellationToken),
        Policy policy => services.GetRequiredService<IPolicyRepository>().AddAsync(policy, cancellationToken),
        ContentItem item => services.GetRequiredService<IContentItemRepository>().AddAsync(item, cancellationToken),
        PropertyEvent propertyEvent => services.GetRequiredService<IPropertyEventRepository>().AddAsync(propertyEvent, cancellationToken),
        _ => throw new NotSupportedException(record.GetType().Name),
    };

    private T Create<T>(Result<T> created)
        where T : IAggregateRoot
    {
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Message : null);
        records.Add(created.Value);

        return created.Value;
    }

    private Policy Active(Result<Policy> created)
    {
        Policy policy = Create(created);
        Assert.True(policy.Activate(Now).IsSuccess);

        return policy;
    }

    private ContentItem Published(Result<ContentItem> created)
    {
        ContentItem item = Create(created);
        Assert.True(item.Publish(Now).IsSuccess);

        return item;
    }
}
