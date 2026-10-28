using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Amenities;
using HospitalityOS.Contracts.Api.Hours;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Journeys 2 and 16 through the real host: hours support closed days, several intervals a day, overnight intervals,
/// date overrides, and temporary closures; they are queryable; an administrator's update emits
/// <c>OperatingHoursUpdated</c> and the concierge's next answer uses it, citing the new version. "Now" is Tuesday
/// 27 October 2026, 09:30 in London.
/// </summary>
public sealed class OperatingHoursEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/operating-hours";

    private static readonly object[] poolWeek =
    [
        new { day = "Tuesday", intervals = new[] { Interval("16:00:00", "22:00:00"), Interval("06:00:00", "10:00:00") } },
        new { day = "Friday", intervals = new[] { Interval("18:00:00", "02:00:00") } },
        new { day = "Monday", intervals = new[] { Interval("06:00:00", "22:00:00") } },
    ];

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Create_StoresTheFullSchedule_Normalized_AndItIsQueryable()
    {
        (PropertyResponse property, AmenityResponse pool) = await PoolAsync();
        using HttpClient admin = api.Staff(tenant);

        OperatingHoursResponse created = await CreateAsync(admin, property, pool);

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Friday], created.Weekly.Select(day => day.Day));
        Assert.Equal([new TimeOnly(6, 0), new TimeOnly(16, 0)], created.Weekly[1].Intervals!.Select(interval => interval.Opens));
        Assert.Equal((new DateOnly(2026, 12, 25), 0, "Christmas"), (created.Overrides[0].Date, created.Overrides[0].Intervals!.Count, created.Overrides[0].Note));
        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 4)), (created.Closures[0].From, created.Closures[0].To));
        Assert.Equal(new SubjectReference(SubjectKind.Amenity, pool.Id), created.Subject);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "OperatingHoursCreated");
        OperatingHoursResponse? read = await admin.GetFromJsonAsync<OperatingHoursResponse>($"{Route}/{created.Id}", ContractJson.Options, Cancellation);
        List<OperatingHoursResponse>? listed = await admin.GetFromJsonAsync<List<OperatingHoursResponse>>($"{Route}?propertyId={property.Id}", ContractJson.Options, Cancellation);
        Assert.Equal(created, read);
        Assert.Equal([created.Id], listed?.Select(hours => hours.Id));
    }

    [Fact]
    public async Task Update_EmitsOperatingHoursUpdated_AndTheConciergesNextAnswerUsesTheNewVersion()
    {
        (PropertyResponse property, AmenityResponse pool) = await PoolAsync();
        using HttpClient admin = api.Staff(tenant);
        OperatingHoursResponse created = await CreateAsync(admin, property, pool);
        object question = new { propertyId = property.Id.Value, subject = "Amenity", subjectId = pool.Id };
        GetOperatingHoursOutput before = await api.AskAsGuestAsync<GetOperatingHoursOutput>(tenant, "Property.GetOperatingHours", question, Cancellation);

        using HttpResponseMessage response = await StaffApi.SendAsync(
            admin, HttpMethod.Put, $"{Route}/{created.Id}", new { weekly = new[] { new { day = "Tuesday", intervals = new[] { Interval("12:00:00", "20:00:00") } } } }, "\"1\"", Cancellation);
        GetOperatingHoursOutput after = await api.AskAsGuestAsync<GetOperatingHoursOutput>(tenant, "Property.GetOperatingHours", question, Cancellation);

        Assert.Equal(2L, (await StaffApi.OkAsync<OperatingHoursResponse>(response, Cancellation)).Version);
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "OperatingHoursUpdated");
        Assert.Equal((1L, true, 2), (before.Hours.Source.Version, before.Hours.IsOpenNow, before.Hours.Periods.Count));
        Assert.Equal((2L, created.Id, false), (after.Hours.Source.Version, after.Hours.Source.Id, after.Hours.IsOpenNow));
        Assert.Equal(new DateTime(2026, 10, 27, 12, 0, 0), Assert.Single(after.Hours.Periods).Start);
    }

    [Fact]
    public async Task TheConcierge_SeesClosuresAndOverrides()
    {
        (PropertyResponse property, AmenityResponse pool) = await PoolAsync();
        using HttpClient admin = api.Staff(tenant);
        await CreateAsync(admin, property, pool);

        GetOperatingHoursOutput closed = await api.AskAsGuestAsync<GetOperatingHoursOutput>(
            tenant, "Property.GetOperatingHours", new { propertyId = property.Id.Value, subject = "Amenity", subjectId = pool.Id, date = "2026-11-03" }, Cancellation);
        GetOperatingHoursOutput christmas = await api.AskAsGuestAsync<GetOperatingHoursOutput>(
            tenant, "Property.GetOperatingHours", new { propertyId = property.Id.Value, subject = "Amenity", subjectId = pool.Id, date = "2026-12-25" }, Cancellation);

        Assert.Equal((HoursBasis.Closure, "Maintenance", 0), (closed.Hours.Basis, closed.Hours.Note, closed.Hours.Periods.Count));
        Assert.Equal((HoursBasis.Override, "Christmas", 0), (christmas.Hours.Basis, christmas.Hours.Note, christmas.Hours.Periods.Count));
    }

    [Fact]
    public async Task Create_ForASubjectThatAlreadyHasHours_Is409_AndAudited()
    {
        (PropertyResponse property, AmenityResponse pool) = await PoolAsync();
        using HttpClient admin = api.Staff(tenant);
        await CreateAsync(admin, property, pool);

        using HttpResponseMessage again = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, subject = new { kind = "Amenity", id = pool.Id }, weekly = poolWeek }, Cancellation);

        Assert.Equal((HttpStatusCode.Conflict, "hours.already_set"), await StaffApi.ProblemAsync(again, Cancellation));
        Assert.Single(await api.EventsAsync(tenant, Cancellation), message => message.EventType == "OperatingHoursCreated");
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Detail) == ("OperatingHours.Create", AuditOutcome.Failed, "hours.already_set"));
    }

    [Fact]
    public async Task Create_WithAnInvalidScheduleAndNoSubject_ReportsEveryField()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        object[] weekly =
        [
            new { day = "Monday", intervals = new[] { Interval("06:00:00", "10:00:00"), Interval("09:00:00", "12:00:00") } },
            new { intervals = Array.Empty<object>() },
            new { day = "Wednesday", intervals = new[] { new { opens = "06:00:00" } } },
        ];

        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, new { propertyId = property.Id.Value, weekly }, Cancellation);

        Assert.Equal(
            ["hours.intervals_overlap", "hours.day_required", "hours.interval_invalid", "hours.subject_required"],
            await StaffApi.FieldCodesAsync(response, Cancellation));
    }

    [Fact]
    public async Task Create_ForASubjectNotInTheProperty_IsASubjectError()
    {
        (PropertyResponse grand, AmenityResponse grandPool) = await PoolAsync();
        PropertyResponse bay = await api.CreatePropertyAsync(tenant, Cancellation, "Aurora Bay");
        using HttpClient admin = api.Staff(tenant);

        Assert.Equal(["hours.subject_not_found"], await CodesAsync(admin, bay, new { kind = "Amenity", id = grandPool.Id }));
        Assert.Equal(["hours.subject_not_found"], await CodesAsync(admin, bay, new { kind = "Property", id = grand.Id.Value }));
        Assert.Equal(["hours.subject_not_found"], await CodesAsync(admin, bay, new { kind = "Restaurant", id = Guid.CreateVersion7() }));
        Assert.Equal(["hours.subject_kind_required"], await CodesAsync(admin, bay, new { id = Guid.CreateVersion7() }));
        Assert.Equal(["hours.subject_id_required"], await CodesAsync(admin, bay, new { kind = "Space" }));
    }

    [Fact]
    public async Task Create_ForThePropertyItself_Succeeds()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route, new { propertyId = property.Id.Value, subject = new { kind = "Property", id = property.Id.Value }, weekly = poolWeek }, Cancellation);

        Assert.Equal(SubjectKind.Property, (await StaffApi.CreatedAsync<OperatingHoursResponse>(response, Cancellation)).Subject.Kind);
    }

    private static object Interval(string opens, string closes) => new { opens, closes };

    private static async Task<IReadOnlyList<string?>> CodesAsync(HttpClient admin, PropertyResponse property, object subject)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(Route, new { propertyId = property.Id.Value, subject, weekly = poolWeek }, Cancellation);

        return await StaffApi.FieldCodesAsync(response, Cancellation);
    }

    private async Task<(PropertyResponse Property, AmenityResponse Pool)> PoolAsync()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/amenities", new { propertyId = property.Id.Value, name = "Pool", category = "Pool" }, Cancellation);

        return (property, await StaffApi.CreatedAsync<AmenityResponse>(response, Cancellation));
    }

    private static async Task<OperatingHoursResponse> CreateAsync(HttpClient admin, PropertyResponse property, AmenityResponse pool)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new
            {
                propertyId = property.Id.Value,
                subject = new { kind = "Amenity", id = pool.Id },
                weekly = poolWeek,
                overrides = new[] { new { date = "2026-12-25", intervals = Array.Empty<object>(), note = "Christmas" } },
                closures = new[] { new { from = "2026-11-02", to = "2026-11-04", reason = "Maintenance" } },
            },
            Cancellation);

        return await StaffApi.CreatedAsync<OperatingHoursResponse>(response, Cancellation);
    }
}
