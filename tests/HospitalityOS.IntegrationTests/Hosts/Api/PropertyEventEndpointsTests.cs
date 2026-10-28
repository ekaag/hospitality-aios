using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Events;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Api.Spaces;
using HospitalityOS.Contracts.Capabilities.Activities;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Journey 8 through the real host: an administrator schedules an event and the concierge offers it; once cancelled it
/// is never offered again and cannot be edited. Times are the property's wall clock (London, GMT on the test date).
/// </summary>
public sealed class PropertyEventEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/events";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_SchedulesTheEvent_AndTheConciergeOffersIt()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse terrace = await CreateSpaceAsync(admin, property, "Terrace");

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new
            {
                propertyId = property.Id.Value,
                title = "Jazz on the Terrace",
                category = "Entertainment",
                localStart = "2026-10-27T19:00:00",
                localEnd = "2026-10-27T22:00:00",
                locationSpaceId = terrace.Id.Value,
                price = new { amount = 15, currencyCode = "GBP" },
                bookingRequired = true,
            },
            Cancellation);

        PropertyEventResponse jazz = await StaffApi.CreatedAsync<PropertyEventResponse>(response, Cancellation);
        Assert.Equal(
            (property.Id, EventAudience.Everyone, new DateTime(2026, 10, 27, 19, 0, 0), PropertyEventStatus.Scheduled, true, 1L),
            (jazz.PropertyId, jazz.Audience, jazz.LocalStart, jazz.Status, jazz.BookingRequired, jazz.Version));
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "PropertyEventCreated" && message.AggregateId == jazz.Id);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.TargetId, audit.PropertyId) == ("PropertyEvent.Create", jazz.Id.ToString(), property.Id));

        EventEntry offered = Assert.Single((await SearchAsync(property)).Events);
        Assert.Equal((jazz.Id, 1L, terrace.Id, true), (offered.Source.Id, offered.Source.Version, offered.LocationSpaceId, offered.BookingRequired));
    }

    [Fact]
    public async Task Cancel_StopsTheConciergeOfferingIt_AndTheEventCanNoLongerBeEdited()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        PropertyEventResponse jazz = await CreateAsync(admin, property, "Jazz on the Terrace", "2026-10-27T19:00:00");

        using HttpResponseMessage cancelled = await StaffApi.SendAsync(admin, HttpMethod.Post, $"{Route}/{jazz.Id}/cancel", null, "\"1\"", Cancellation);
        using HttpResponseMessage edit = await admin.PutAsJsonAsync(
            $"{Route}/{jazz.Id}", new { title = "Jazz", category = "Entertainment", localStart = "2026-10-28T19:00:00", localEnd = "2026-10-28T22:00:00" }, Cancellation);

        PropertyEventResponse called = await StaffApi.OkAsync<PropertyEventResponse>(cancelled, Cancellation);
        Assert.Equal((PropertyEventStatus.Cancelled, 2L), (called.Status, called.Version));
        Assert.Empty((await SearchAsync(property)).Events);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "property_event.cancelled"), await StaffApi.ProblemAsync(edit, Cancellation));
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome) == ("PropertyEvent.Cancel", AuditOutcome.Succeeded));
    }

    [Fact]
    public async Task Update_MovesTheEvent_AndTheConciergeCitesTheNewVersion()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        PropertyEventResponse yoga = await CreateAsync(admin, property, "Sunrise Yoga", "2026-10-28T07:00:00");

        using HttpResponseMessage response = await StaffApi.SendAsync(
            admin,
            HttpMethod.Put,
            $"{Route}/{yoga.Id}",
            new { title = "Sunrise Yoga", category = "Wellness", audience = "AdultsOnly", localStart = "2026-10-28T08:00:00", localEnd = "2026-10-28T09:00:00" },
            "\"1\"",
            Cancellation);

        PropertyEventResponse moved = await StaffApi.OkAsync<PropertyEventResponse>(response, Cancellation);
        Assert.Equal((PropertyEventCategory.Wellness, EventAudience.AdultsOnly, new DateTime(2026, 10, 28, 8, 0, 0), 2L), (moved.Category, moved.Audience, moved.LocalStart, moved.Version));
        EventEntry offered = Assert.Single((await SearchAsync(property)).Events);
        Assert.Equal((2L, new DateTime(2026, 10, 28, 8, 0, 0)), (offered.Source.Version, offered.LocalStart));
    }

    [Fact]
    public async Task Create_WithTimesGivenWithAnOffset_IsRejected_RatherThanStoredShifted()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new { propertyId = property.Id.Value, title = "Jazz", category = "Entertainment", localStart = "2026-10-27T19:00:00Z", localEnd = "2026-10-27T22:00:00+01:00" },
            Cancellation);

        Assert.Equal(["property_event.start_not_local", "property_event.end_not_local"], await StaffApi.FieldCodesAsync(response, Cancellation));
    }

    [Fact]
    public async Task Create_WithoutAnEnd_InAnotherPropertysSpace_ReportsOnlyTheRealProblems()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        SpaceResponse grandTerrace = await CreateSpaceAsync(admin, grand, "Terrace");

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = bay.Id.Value, title = "Jazz", localStart = "2026-10-27T19:00:00", locationSpaceId = grandTerrace.Id.Value }, Cancellation);

        Assert.Equal(["property_event.end_required", "property_event.category_required", "property_event.location_not_found"], await StaffApi.FieldCodesAsync(response, Cancellation));
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome) == ("PropertyEvent.Create", AuditOutcome.Failed));
    }

    [Fact]
    public async Task List_ShowsEveryEventOfTheProperty_PastAndCancelledIncluded_EarliestFirst()
    {
        PropertyResponse grand = await api.CreatePropertyAsync(tenant, Cancellation);
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);
        PropertyEventResponse jazz = await CreateAsync(admin, grand, "Jazz", "2026-10-27T19:00:00");
        await CreateAsync(admin, grand, "Yesterday's quiz", "2026-10-26T19:00:00");
        await CreateAsync(admin, bay, "Bay regatta", "2026-10-27T10:00:00");
        using HttpResponseMessage cancelled = await admin.PostAsync($"{Route}/{jazz.Id}/cancel", null, Cancellation);
        using HttpClient worker = api.Staff(tenant, "Worker", "sam");

        IReadOnlyList<PropertyEventResponse>? listed = await worker.GetFromJsonAsync<IReadOnlyList<PropertyEventResponse>>($"{Route}?propertyId={grand.Id}", ContractJson.Options, Cancellation);

        Assert.Equal([("Yesterday's quiz", PropertyEventStatus.Scheduled), ("Jazz", PropertyEventStatus.Cancelled)], listed!.Select(entry => (entry.Title, entry.Status)));
    }

    [Fact]
    public async Task Cancel_AsManager_Is403_AndAuditedAsDenied()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        PropertyEventResponse jazz = await CreateAsync(admin, property, "Jazz", "2026-10-27T19:00:00");
        using HttpClient manager = api.Staff(tenant, "Manager", "lee");

        using HttpResponseMessage response = await manager.PostAsync($"{Route}/{jazz.Id}/cancel", null, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single((await SearchAsync(property)).Events);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Actor.Id) == ("PropertyEvent.Cancel", AuditOutcome.Denied, "lee"));
    }

    private Task<SearchPropertyEventsOutput> SearchAsync(PropertyResponse property) =>
        api.AskAsGuestAsync<SearchPropertyEventsOutput>(tenant, "Events.SearchPropertyEvents", new { propertyId = property.Id.Value }, Cancellation);

    private static async Task<PropertyEventResponse> CreateAsync(HttpClient admin, PropertyResponse property, string title, string localStart)
    {
        DateTime start = DateTime.Parse(localStart, System.Globalization.CultureInfo.InvariantCulture);
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, title, category = "Entertainment", localStart, localEnd = start.AddHours(2).ToString("s", System.Globalization.CultureInfo.InvariantCulture) }, Cancellation);

        return await StaffApi.CreatedAsync<PropertyEventResponse>(response, Cancellation);
    }

    private static async Task<SpaceResponse> CreateSpaceAsync(HttpClient admin, PropertyResponse property, string name)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/spaces", new { propertyId = property.Id.Value, name, kind = "Area" }, Cancellation);

        return await StaffApi.CreatedAsync<SpaceResponse>(response, Cancellation);
    }
}
