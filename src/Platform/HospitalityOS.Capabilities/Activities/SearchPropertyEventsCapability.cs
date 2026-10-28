using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.Capabilities.Reading;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Domain.Properties.Properties;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;
using ContractAudience = HospitalityOS.Contracts.Capabilities.Activities.EventAudience;
using ContractCategory = HospitalityOS.Contracts.Capabilities.Activities.PropertyEventCategory;
using DomainAudience = HospitalityOS.Domain.Activities.PropertyEvents.EventAudience;
using DomainCategory = HospitalityOS.Domain.Activities.PropertyEvents.PropertyEventCategory;

namespace HospitalityOS.Capabilities.Activities;

/// <summary>
/// <c>Events.SearchPropertyEvents</c>: what is on at the property, earliest first; past and cancelled events are never
/// offered (Journey 8).
/// </summary>
internal sealed class SearchPropertyEventsCapability(
    IPropertyRepository properties,
    IPropertyEventRepository propertyEvents,
    TimeProvider timeProvider) : ICapability<SearchPropertyEventsInput, SearchPropertyEventsOutput>
{
    public const int MaxLimit = 50;

    public static CapabilityDescriptor Descriptor { get; } = ReadCapability.Describe(
        "Events.SearchPropertyEvents",
        "Lists upcoming and ongoing events and activities at the property, earliest first, optionally within property-local "
            + "times and by category or audience (e.g. tonight's events for families). Past and cancelled events are never "
            + "returned. If an event requires booking, explain how; never imply a booking was made.");

    public async Task<Result<SearchPropertyEventsOutput>> ExecuteAsync(SearchPropertyEventsInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Limit is < 1 or > MaxLimit)
        {
            return ReadErrors.OutOfRange("limit", 1, MaxLimit);
        }

        Result<PropertyClock> loaded = await PropertyClock.LoadAsync(properties, input.PropertyId, timeProvider, cancellationToken).ConfigureAwait(false);

        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        PropertyClock clock = loaded.Value;
        DateTime from = input.From is { } requested && requested > clock.LocalNow ? requested : clock.LocalNow;
        IReadOnlyList<PropertyEvent> candidates = await propertyEvents.ListScheduledEndingAfterAsync(input.PropertyId, from, cancellationToken).ConfigureAwait(false);
        PropertyEventFilter filter = new(
            from,
            input.To,
            input.Category is { } category ? EnumMirror.To<DomainCategory>(category) : null,
            input.Audience is { } audience ? EnumMirror.To<DomainAudience>(audience) : null);

        IEnumerable<PropertyEvent> upcoming = PropertyEvent.SelectUpcoming(candidates, clock.Now, clock.TimeZone, filter).Take(input.Limit);

        return new SearchPropertyEventsOutput(
            clock.Property.Details.TimeZoneId,
            clock.LocalNow,
            ValueList.From(upcoming.Select(propertyEvent => new EventEntry(
                Sources.Of(propertyEvent),
                propertyEvent.Details.Title,
                EnumMirror.To<ContractCategory>(propertyEvent.Details.Category),
                EnumMirror.To<ContractAudience>(propertyEvent.Details.Audience),
                propertyEvent.Details.LocalStart,
                propertyEvent.Details.LocalEnd,
                propertyEvent.Details.Description,
                propertyEvent.Details.LocationSpaceId,
                propertyEvent.Details.Price,
                propertyEvent.Details.BookingRequired))));
    }
}
