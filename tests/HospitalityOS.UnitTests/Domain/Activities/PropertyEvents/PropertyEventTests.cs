using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Activities.PropertyEvents.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Activities.PropertyEvents;

public sealed class PropertyEventTests
{
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209911001"));
    private static readonly PropertyId propertyId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209911002"));
    private static readonly TimeZoneInfo losAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    // Saturday 2026-10-24, 17:00 in Los Angeles (PDT, UTC-7).
    private static readonly DateTimeOffset now = new(2026, 10, 24, 17, 0, 0, TimeSpan.FromHours(-7));
    private static readonly DateTime localNow = new(2026, 10, 24, 17, 0, 0);

    [Fact]
    public void Create_WithValidDetails_SchedulesAndRaisesCreated()
    {
        PropertyEventDetails jazz = Jazz();

        PropertyEvent propertyEvent = PropertyEvent.Create(Id(1), tenantId, propertyId, jazz, now).Value;

        Assert.Equal(PropertyEventStatus.Scheduled, propertyEvent.Status);
        Assert.Equal([new PropertyEventCreated(Id(1), propertyId, jazz, PropertyEventStatus.Scheduled, now)], propertyEvent.DomainEvents);
    }

    [Fact]
    public void Create_DropsDateTimeKindFromLocalTimes()
    {
        PropertyEventDetails utcKinds = Jazz() with
        {
            LocalStart = DateTime.SpecifyKind(localNow.AddHours(2), DateTimeKind.Utc),
            LocalEnd = DateTime.SpecifyKind(localNow.AddHours(4), DateTimeKind.Local),
        };

        PropertyEvent propertyEvent = PropertyEvent.Create(Id(1), tenantId, propertyId, utcKinds, now).Value;

        Assert.Equal(DateTimeKind.Unspecified, propertyEvent.Details.LocalStart.Kind);
        Assert.Equal(DateTimeKind.Unspecified, propertyEvent.Details.LocalEnd.Kind);
        Assert.Equal(localNow.AddHours(2), propertyEvent.Details.LocalStart);
    }

    [Fact]
    public void Create_WithInvalidFields_ReportsAll()
    {
        PropertyEventDetails invalid = new(
            "", (PropertyEventCategory)99, (EventAudience)99, localNow, localNow.AddHours(-1), Price: new Money(5m, "dollars"));

        Result<PropertyEvent> result = PropertyEvent.Create(Id(1), tenantId, propertyId, invalid, now);

        ValidationError error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(
            [
                ("title", "property_event.title_required"),
                ("price.currencyCode", "property_event.price_currency_invalid"),
                ("category", "property_event.category_invalid"),
                ("audience", "property_event.audience_invalid"),
                ("localEnd", "property_event.end_before_start"),
            ],
            error.Fields.Select(field => (field.Field, field.Code)));
    }

    [Fact]
    public void Create_LongerThanMaxDuration_ReturnsValidationError()
    {
        Result<PropertyEvent> result = PropertyEvent.Create(Id(1), tenantId, propertyId, Jazz() with { LocalEnd = localNow.AddDays(15) }, now);

        Assert.Equal("property_event.too_long", Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields).Code);
    }

    [Fact]
    public void SelectUpcoming_ExcludesPastAndCancelled_IncludesOngoing_EarliestFirst()
    {
        PropertyEvent yogaThisMorning = Schedule(1, "Sunrise yoga", localNow.AddHours(-10), localNow.AddHours(-9), PropertyEventCategory.Fitness);
        PropertyEvent poolPartyNow = Schedule(2, "Pool party", localNow.AddHours(-1), localNow.AddHours(1), PropertyEventCategory.Social);
        PropertyEvent jazzTonight = Schedule(3, "Jazz on the Terrace", localNow.AddHours(3), localNow.AddHours(5));
        PropertyEvent cancelledMovie = Schedule(4, "Movie night", localNow.AddHours(2), localNow.AddHours(4));
        cancelledMovie.Cancel(now);

        IReadOnlyList<PropertyEvent> upcoming = PropertyEvent.SelectUpcoming([jazzTonight, cancelledMovie, yogaThisMorning, poolPartyNow], now, losAngeles);

        Assert.Equal([poolPartyNow, jazzTonight], upcoming);
    }

    [Fact]
    public void SelectUpcoming_Tonight_FiltersByTimeWindow()
    {
        PropertyEvent jazzTonight = Schedule(1, "Jazz on the Terrace", localNow.AddHours(3), localNow.AddHours(5));
        PropertyEvent brunchTomorrow = Schedule(2, "Sunday brunch", localNow.AddHours(17), localNow.AddHours(20), PropertyEventCategory.FoodAndDrink);
        PropertyEventFilter tonight = new(LocalTo: localNow.Date.AddDays(1));

        IReadOnlyList<PropertyEvent> result = PropertyEvent.SelectUpcoming([jazzTonight, brunchTomorrow], now, losAngeles, tonight);

        Assert.Equal([jazzTonight], result);
    }

    [Fact]
    public void SelectUpcoming_ByAudience_IncludesEveryoneEvents()
    {
        PropertyEvent kidsClub = Schedule(1, "Kids' club", localNow.AddHours(1), localNow.AddHours(2), PropertyEventCategory.Family, EventAudience.Children);
        PropertyEvent wineTasting = Schedule(2, "Wine tasting", localNow.AddHours(1), localNow.AddHours(2), PropertyEventCategory.FoodAndDrink, EventAudience.AdultsOnly);
        PropertyEvent jazz = Schedule(3, "Jazz on the Terrace", localNow.AddHours(3), localNow.AddHours(5));

        IReadOnlyList<PropertyEvent> forChildren = PropertyEvent.SelectUpcoming([kidsClub, wineTasting, jazz], now, losAngeles, new PropertyEventFilter(Audience: EventAudience.Children));

        Assert.Equal([kidsClub, jazz], forChildren);
    }

    [Fact]
    public void SelectUpcoming_ByCategory_ReturnsOnlyThatCategory()
    {
        PropertyEvent spin = Schedule(1, "Spin class", localNow.AddHours(1), localNow.AddHours(2), PropertyEventCategory.Fitness);
        PropertyEvent jazz = Schedule(2, "Jazz on the Terrace", localNow.AddHours(3), localNow.AddHours(5));

        IReadOnlyList<PropertyEvent> fitness = PropertyEvent.SelectUpcoming([spin, jazz], now, losAngeles, new PropertyEventFilter(Category: PropertyEventCategory.Fitness));

        Assert.Equal([spin], fitness);
    }

    [Fact]
    public void SelectUpcoming_UsesPropertyLocalTime_NotUtc()
    {
        // 18:00 in Los Angeles is 01:00 UTC the next day. Comparing UTC wall-clock time against local event times
        // would wrongly treat this 17:00-19:00 local event as already over.
        PropertyEvent ongoingLocally = Schedule(1, "Sunset cocktails", localNow, localNow.AddHours(2));
        DateTimeOffset sixPmLocalAsUtc = now.AddHours(1).ToUniversalTime();

        IReadOnlyList<PropertyEvent> result = PropertyEvent.SelectUpcoming([ongoingLocally], sixPmLocalAsUtc, losAngeles);

        Assert.Equal([ongoingLocally], result);
    }

    [Fact]
    public void Cancel_RaisesUpdatedOnce_AndBlocksFurtherEdits()
    {
        PropertyEvent jazz = Schedule(1, "Jazz on the Terrace", localNow.AddHours(3), localNow.AddHours(5));

        jazz.Cancel(now);
        jazz.Cancel(now.AddMinutes(1));
        Result update = jazz.Update(jazz.Details with { Title = "Jazz (moved indoors)" }, now.AddMinutes(2));

        Assert.Equal(ErrorKind.InvalidState, update.Error.Kind);
        PropertyEventUpdated cancelled = Assert.IsType<PropertyEventUpdated>(Assert.Single(jazz.DomainEvents));
        Assert.Equal(PropertyEventStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void Update_WithChangedTime_RaisesUpdated_AndSameDetailsIsNoOp()
    {
        PropertyEvent jazz = Schedule(1, "Jazz on the Terrace", localNow.AddHours(3), localNow.AddHours(5));
        PropertyEventDetails later = jazz.Details with { LocalStart = localNow.AddHours(4), LocalEnd = localNow.AddHours(6) };

        jazz.Update(later, now);
        jazz.Update(later with { Title = " Jazz on the Terrace " }, now);

        Assert.Equal([new PropertyEventUpdated(Id(1), later, PropertyEventStatus.Scheduled, now)], jazz.DomainEvents);
    }

    private static PropertyEventDetails Jazz() => new(
        "Jazz on the Terrace",
        PropertyEventCategory.Entertainment,
        EventAudience.Everyone,
        localNow.AddHours(3),
        localNow.AddHours(5),
        "Live quartet with sunset views.",
        Price: new Money(0m, "USD"),
        BookingRequired: true);

    private static PropertyEvent Schedule(
        int idSuffix,
        string title,
        DateTime localStart,
        DateTime localEnd,
        PropertyEventCategory category = PropertyEventCategory.Entertainment,
        EventAudience audience = EventAudience.Everyone)
    {
        PropertyEvent propertyEvent = PropertyEvent
            .Create(Id(idSuffix), tenantId, propertyId, new PropertyEventDetails(title, category, audience, localStart, localEnd), now)
            .Value;
        propertyEvent.DequeueDomainEvents();

        return propertyEvent;
    }

    private static PropertyEventId Id(int suffix) => new(Guid.Parse($"01890a5d-ac96-774b-bcce-b302099{suffix:D5}"));
}
