using HospitalityOS.Application.Administration;
using HospitalityOS.Application.Auditing;
using HospitalityOS.Contracts;
using HospitalityOS.Contracts.Api.Events;
using HospitalityOS.Domain.Activities.PropertyEvents;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using ContractAudience = HospitalityOS.Contracts.Capabilities.Activities.EventAudience;
using ContractCategory = HospitalityOS.Contracts.Capabilities.Activities.PropertyEventCategory;
using ContractStatus = HospitalityOS.Contracts.Api.Events.PropertyEventStatus;
using DomainAudience = HospitalityOS.Domain.Activities.PropertyEvents.EventAudience;
using DomainCategory = HospitalityOS.Domain.Activities.PropertyEvents.PropertyEventCategory;

namespace HospitalityOS.Application.Events;

internal sealed class PropertyEventAdministration(
    IPropertyEventRepository propertyEvents,
    PropertyReferences references,
    ICurrentTenant currentTenant,
    AuditedCommit commit,
    RecordIds ids,
    TimeProvider timeProvider) : IPropertyEventAdministration
{
    public async Task<Result<PropertyEventResponse>> CreateAsync(CreatePropertyEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PropertyId> property = await references.PropertyAsync(request.PropertyId, "property_event", cancellationToken).ConfigureAwait(false);

        if (property.IsFailure)
        {
            return await commit.RejectAsync(AdministrationActions.PropertyEventCreate, property.Error, null, cancellationToken).ConfigureAwait(false);
        }

        RequestFields fields = new();
        PropertyEventDetails details = await DetailsAsync(
            property.Value,
            new EventFields(request.Title, request.Category, request.Audience, request.LocalStart, request.LocalEnd, request.Description, request.LocationSpaceId, request.Price, request.BookingRequired),
            fields,
            cancellationToken).ConfigureAwait(false);
        PropertyEventId id = new(ids.Next());
        Result<PropertyEvent> created = fields.Merge(PropertyEvent.Create(id, currentTenant.TenantId, property.Value, details, timeProvider.GetUtcNow()));

        return await commit.CreateAsync(
            AdministrationActions.PropertyEventCreate,
            created,
            propertyEvent => propertyEvents.AddAsync(propertyEvent, cancellationToken),
            propertyEvent => Target(propertyEvent.Id.Value, propertyEvent.PropertyId),
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<PropertyEventResponse>> UpdateAsync(Guid id, UpdatePropertyEventRequest request, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ChangeAsync(
            AdministrationActions.PropertyEventUpdate,
            id,
            expectedVersion,
            async propertyEvent =>
            {
                RequestFields fields = new();
                PropertyEventDetails details = await DetailsAsync(
                    propertyEvent.PropertyId,
                    new EventFields(request.Title, request.Category, request.Audience, request.LocalStart, request.LocalEnd, request.Description, request.LocationSpaceId, request.Price, request.BookingRequired),
                    fields,
                    cancellationToken).ConfigureAwait(false);

                return fields.Apply(() => propertyEvent.Update(details, timeProvider.GetUtcNow()));
            },
            cancellationToken);
    }

    public Task<Result<PropertyEventResponse>> CancelAsync(Guid id, long? expectedVersion, CancellationToken cancellationToken) =>
        ChangeAsync(AdministrationActions.PropertyEventCancel, id, expectedVersion, propertyEvent => Task.FromResult(propertyEvent.Cancel(timeProvider.GetUtcNow())), cancellationToken);

    public async Task<Result<PropertyEventResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        PropertyEvent? propertyEvent = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return propertyEvent is null ? AdministrationErrors.PropertyEventNotFound : Response(propertyEvent);
    }

    public async Task<IReadOnlyList<PropertyEventResponse>> ListAsync(PropertyId propertyId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PropertyEvent> found = await propertyEvents.ListForPropertyAsync(propertyId, cancellationToken).ConfigureAwait(false);

        return [.. found
            .OrderBy(propertyEvent => propertyEvent.Details.LocalStart)
            .ThenBy(propertyEvent => propertyEvent.Details.Title, StringComparer.OrdinalIgnoreCase)
            .Select(Response)];
    }

    private async Task<Result<PropertyEventResponse>> ChangeAsync(
        string action,
        Guid id,
        long? expectedVersion,
        Func<PropertyEvent, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        PropertyEvent? propertyEvent = await FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await commit.ChangeAsync(
            action,
            Target(id, propertyEvent?.PropertyId),
            propertyEvent,
            AdministrationErrors.PropertyEventNotFound,
            expectedVersion,
            change,
            Response,
            cancellationToken).ConfigureAwait(false);
    }

    // An empty GUID cannot be an event ID, so it names no event.
    private Task<PropertyEvent?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        id == Guid.Empty ? Task.FromResult<PropertyEvent?>(null) : propertyEvents.GetByIdAsync(new PropertyEventId(id), cancellationToken);

    private static AuditTarget Target(Guid id, PropertyId? propertyId) => new(nameof(PropertyEvent), id.ToString(), propertyId);

    // The domain validates every field, including missing text, so absent values pass through as null.
    private async Task<PropertyEventDetails> DetailsAsync(PropertyId propertyId, EventFields request, RequestFields fields, CancellationToken cancellationToken)
    {
        (DateTime start, DateTime end) = Times(request.LocalStart, request.LocalEnd, fields);

        return new PropertyEventDetails(
            request.Title!,
            fields.Required<DomainCategory>(request.Category, "category", "property_event.category_required"),
            request.Audience is { } audience ? EnumMirror.To<DomainAudience>(audience) : DomainAudience.Everyone,
            start,
            end,
            request.Description,
            await references.LocationAsync(propertyId, request.LocationSpaceId, "locationSpaceId", "property_event.location_not_found", fields, cancellationToken).ConfigureAwait(false),
            request.Price,
            request.BookingRequired ?? false);
    }

    // A missing or unusable time is recorded and replaced by one an hour from the other, so the domain reports only real
    // problems with the times given.
    private static (DateTime Start, DateTime End) Times(DateTime? localStart, DateTime? localEnd, RequestFields fields)
    {
        DateTime? start = LocalTime(localStart, "localStart", "property_event.start", fields);
        DateTime? end = LocalTime(localEnd, "localEnd", "property_event.end", fields);

        return (start, end) switch
        {
            ({ } given, { } givenEnd) => (given, givenEnd),
            ({ } only, null) => (only, only < DateTime.MaxValue.AddHours(-1) ? only.AddHours(1) : only),
            (null, { } only) => (only > DateTime.MinValue.AddHours(1) ? only.AddHours(-1) : only, only),
            _ => (DateTime.MinValue, DateTime.MinValue.AddHours(1)),
        };
    }

    // Times are the property's wall clock. A value written with an offset or Z was converted on reading, so its wall-clock
    // time is lost: refuse it rather than store a shifted time.
    private static DateTime? LocalTime(DateTime? value, string field, string codePrefix, RequestFields fields)
    {
        if (value is not { } time)
        {
            fields.Add(field, codePrefix + "_required", $"{field} is required.");

            return null;
        }

        if (time.Kind != DateTimeKind.Unspecified)
        {
            fields.Add(field, codePrefix + "_not_local", $"{field} must be the property's local time, written without an offset or 'Z'.");

            return null;
        }

        return time;
    }

    private static PropertyEventResponse Response(PropertyEvent propertyEvent)
    {
        PropertyEventDetails details = propertyEvent.Details;

        return new PropertyEventResponse(
            propertyEvent.Id.Value,
            propertyEvent.PropertyId,
            details.Title,
            EnumMirror.To<ContractCategory>(details.Category),
            EnumMirror.To<ContractAudience>(details.Audience),
            details.LocalStart,
            details.LocalEnd,
            details.Description,
            details.LocationSpaceId,
            details.Price,
            details.BookingRequired,
            EnumMirror.To<ContractStatus>(propertyEvent.Status),
            propertyEvent.Version);
    }

    /// <summary>
    /// The fields create and update requests share.
    /// </summary>
    private sealed record EventFields(
        string? Title,
        ContractCategory? Category,
        ContractAudience? Audience,
        DateTime? LocalStart,
        DateTime? LocalEnd,
        string? Description,
        SpaceId? LocationSpaceId,
        Money? Price,
        bool? BookingRequired);
}
