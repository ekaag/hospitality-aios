using HospitalityOS.Contracts.Events.Values;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Dining.Menus;
using HospitalityOS.Domain.Dining.Restaurants;
using HospitalityOS.Domain.Properties.AccommodationTypes;
using HospitalityOS.Domain.Properties.Amenities;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.Domain.Properties.Hours;
using HospitalityOS.Domain.Properties.Policies;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.Domain.Properties.Shared;
using HospitalityOS.Domain.Properties.Spaces;
using HospitalityOS.Domain.Services.ServiceRequests;
using HospitalityOS.Domain.Services.Services;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Eventing.Contracts;

/// <summary>
/// Domain value objects to their contract records: enums become their names, domain-local IDs become GUIDs, and
/// optional lists become empty lists, so consumers never see a domain type (P9).
/// </summary>
internal static class ContractValues
{
    public static PropertyDetailsData Data(PropertyDetails details) => new(
        details.Name,
        details.TimeZoneId,
        new PostalAddressData(details.Address.Line1, details.Address.Line2, details.Address.City, details.Address.Region, details.Address.PostalCode, details.Address.CountryCode),
        details.ContactPhone,
        details.ContactEmail);

    public static SpaceDetailsData Data(SpaceDetails details) =>
        new(details.Name, details.Kind.ToString(), details.Code, details.Description, details.Directions);

    public static AccommodationTypeDetailsData Data(AccommodationTypeDetails details) =>
        new(details.Name, details.MaxOccupancy, details.Description);

    public static AmenityDetailsData Data(AmenityDetails details) =>
        new(details.Name, details.Category.ToString(), details.Description);

    public static SubjectReferenceData Data(SubjectReference subject) => new(subject.Kind.ToString(), subject.Id);

    public static ContentItemDetailsData Data(ContentItemDetails details) => new(
        details.Type.ToString(),
        details.Title,
        details.Body,
        details.About is null ? null : Data(details.About),
        details.Tags ?? []);

    public static OperatingHoursScheduleData Data(OperatingHoursSchedule schedule) => new(
        List(schedule.Weekly, day => new DayHoursData(day.Day.ToString(), Intervals(day.Intervals))),
        List(schedule.Overrides, dateOverride => new DateOverrideData(dateOverride.Date, Intervals(dateOverride.Intervals), dateOverride.Note)),
        List(schedule.Closures, closure => new TemporaryClosureData(closure.From, closure.To, closure.Reason)));

    public static PolicyDetailsData Data(PolicyDetails details) => new(
        details.Type.ToString(),
        details.Title,
        details.Content,
        details.EffectiveFrom,
        details.EffectiveTo,
        details.AppliesTo is null ? null : Data(details.AppliesTo));

    public static RestaurantDetailsData Data(RestaurantDetails details) =>
        new(details.Name, details.Cuisine, details.Description, details.LocationSpaceId);

    public static MenuDetailsData Data(MenuDetails details) => new(
        details.Name,
        List(details.Items, item => new MenuItemData(
            item.Id.Value,
            item.Name,
            item.Description,
            item.Price,
            item.Category,
            List(item.Dietary ?? [], tag => tag.ToString()),
            item.IsAvailable)),
        details.Description);

    public static PropertyEventDetailsData Data(PropertyEventDetails details) => new(
        details.Title,
        details.Category.ToString(),
        details.Audience.ToString(),
        details.LocalStart,
        details.LocalEnd,
        details.Description,
        details.LocationSpaceId,
        details.Price,
        details.BookingRequired);

    public static ServiceDetailsData Data(ServiceDetails details) => new(
        details.Code,
        details.Name,
        details.Category.ToString(),
        details.Description,
        details.IsGuestRequestable,
        List(details.Items ?? [], item => new ServiceItemData(item.Code, item.Name, item.MaxQuantityPerRequest)),
        details.TargetCompletion);

    public static ServiceRequestDetailsData Data(ServiceRequestDetails details) =>
        new(details.Location, details.ItemCode, details.Quantity, details.Description);

    public static RequestOriginData Data(RequestOrigin origin) =>
        new(origin.GuestId, origin.StayContextId, origin.ConversationId, origin.SourceMessageId);

    private static ValueList<TimeIntervalData> Intervals(ValueList<TimeInterval> intervals) =>
        List(intervals, interval => new TimeIntervalData(interval.Opens, interval.Closes));

    private static ValueList<TResult> List<T, TResult>(IEnumerable<T> items, Func<T, TResult> map) => ValueList.From(items.Select(map));
}
