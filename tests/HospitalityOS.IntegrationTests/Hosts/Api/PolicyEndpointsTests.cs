using System.Net;
using System.Net.Http.Json;
using HospitalityOS.Contracts.Api.Amenities;
using HospitalityOS.Contracts.Api.Policies;
using HospitalityOS.Contracts.Api.Properties;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;
using ContractSubject = HospitalityOS.Contracts.Api.Properties.SubjectReference;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Journey 4 through the real host: an administrator creates and updates a policy's type, title, content, effective
/// dates, property, and status; updates emit <c>PolicyUpdated</c>; only active policies reach the concierge.
/// </summary>
public sealed class PolicyEndpointsTests : IAsyncDisposable
{
    private const string Route = "/api/v1/policies";

    private readonly StaffApi api = new();
    private readonly TenantId tenant = new(Guid.CreateVersion7());

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task Lifecycle_DraftActivateUpdateRetire_EmitsEachChange_AndRetiredIsFinal()
    {
        (PropertyResponse property, AmenityResponse gym) = await GymAsync();
        using HttpClient admin = api.Staff(tenant);

        PolicyResponse draft = await CreateAsync(admin, property, gym);
        PolicyResponse active = await StaffApi.OkAsync<PolicyResponse>(await StaffApi.SendAsync(admin, HttpMethod.Post, $"{Route}/{draft.Id}/activate", null, "\"1\"", Cancellation), Cancellation);
        PolicyResponse edited = await StaffApi.OkAsync<PolicyResponse>(
            await admin.PutAsJsonAsync($"{Route}/{draft.Id}", new { type = "AgeRestriction", title = "Gym age policy", content = "Under-16s must be accompanied by an adult.", effectiveFrom = "2026-11-01" }, Cancellation),
            Cancellation);
        PolicyResponse retired = await StaffApi.OkAsync<PolicyResponse>(await admin.PostAsync($"{Route}/{draft.Id}/retire", null, Cancellation), Cancellation);
        using HttpResponseMessage editRetired = await admin.PutAsJsonAsync($"{Route}/{draft.Id}", new { type = "AgeRestriction", title = "Gym", content = "Anyone." }, Cancellation);
        using HttpResponseMessage reactivate = await admin.PostAsync($"{Route}/{draft.Id}/activate", null, Cancellation);

        Assert.Equal(
            [(PolicyStatus.Draft, 1L), (PolicyStatus.Active, 2L), (PolicyStatus.Active, 3L), (PolicyStatus.Retired, 4L)],
            new[] { draft, active, edited, retired }.Select(policy => (policy.Status, policy.Version)));
        Assert.Equal(("Under-16s must be accompanied by an adult.", new DateOnly(2026, 11, 1), null), (edited.Content, edited.EffectiveFrom, edited.AppliesTo));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "policy.retired"), await StaffApi.ProblemAsync(editRetired, Cancellation));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "policy.retired"), await StaffApi.ProblemAsync(reactivate, Cancellation));
        Assert.Equal(
            ["PolicyCreated", "PolicyUpdated", "PolicyUpdated", "PolicyUpdated"],
            (await api.EventsAsync(tenant, Cancellation)).Where(message => message.EventType.StartsWith("Policy", StringComparison.Ordinal)).Select(message => message.EventType));
    }

    [Fact]
    public async Task OnlyAnActivePolicy_ReachesTheConcierge_CitingItsVersion()
    {
        (PropertyResponse property, AmenityResponse gym) = await GymAsync();
        using HttpClient admin = api.Staff(tenant);
        PolicyResponse draft = await CreateAsync(admin, property, gym);
        object question = new { propertyId = property.Id.Value, subjectId = gym.Id };

        GetPolicyOutput whileDraft = await api.AskAsGuestAsync<GetPolicyOutput>(tenant, "Property.GetPolicy", question, Cancellation);
        using HttpResponseMessage activated = await admin.PostAsync($"{Route}/{draft.Id}/activate", null, Cancellation);
        GetPolicyOutput whileActive = await api.AskAsGuestAsync<GetPolicyOutput>(tenant, "Property.GetPolicy", question, Cancellation);

        Assert.Empty(whileDraft.Policies);
        PolicyEntry entry = Assert.Single(whileActive.Policies);
        Assert.Equal((draft.Id, 2L, "Children in the gym", new Subject(SubjectKind.Amenity, gym.Id)), (entry.Source.Id, entry.Source.Version, entry.Title, entry.AppliesTo));
    }

    [Fact]
    public async Task Create_WithMissingAndInvalidFields_ReportsEveryField_AndSavesNothing()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);

        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new { propertyId = property.Id.Value, title = " ", effectiveFrom = "2026-12-01", effectiveTo = "2026-11-01", appliesTo = new { kind = "Amenity", id = Guid.CreateVersion7() } },
            Cancellation);

        Assert.Equal(
            ["policy.title_required", "policy.content_required", "policy.effective_range_invalid", "policy.type_required", "policy.applies_to_not_found"],
            await StaffApi.FieldCodesAsync(response, Cancellation));
        Assert.Empty(await admin.GetFromJsonAsync<List<PolicyResponse>>($"{Route}?propertyId={property.Id}", ContractJson.Options, Cancellation) ?? []);
    }

    [Fact]
    public async Task Activate_AsManager_Is403_AuditedAsDenied_AndTheDraftStays()
    {
        (PropertyResponse property, AmenityResponse gym) = await GymAsync();
        using HttpClient admin = api.Staff(tenant);
        PolicyResponse draft = await CreateAsync(admin, property, gym);
        using HttpClient manager = api.Staff(tenant, "Manager", "lee");

        using HttpResponseMessage response = await manager.PostAsync($"{Route}/{draft.Id}/activate", null, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(await api.AuditAsync(tenant, Cancellation), audit => (audit.Action, audit.Outcome, audit.Actor.Id) == ("Policy.Activate", AuditOutcome.Denied, "lee"));
        Assert.Equal(PolicyStatus.Draft, (await manager.GetFromJsonAsync<PolicyResponse>($"{Route}/{draft.Id}", ContractJson.Options, Cancellation))?.Status);
    }

    [Fact]
    public async Task List_ShowsEveryStatus_ByTitle()
    {
        (PropertyResponse property, AmenityResponse gym) = await GymAsync();
        using HttpClient admin = api.Staff(tenant);
        PolicyResponse children = await CreateAsync(admin, property, gym);
        using HttpResponseMessage pets = await admin.PostAsJsonAsync(Route, new { propertyId = property.Id.Value, type = "Pets", title = "Pets", content = "Dogs welcome." }, Cancellation);
        using HttpResponseMessage activated = await admin.PostAsync($"{Route}/{children.Id}/activate", null, Cancellation);

        List<PolicyResponse>? listed = await admin.GetFromJsonAsync<List<PolicyResponse>>($"{Route}?propertyId={property.Id}", ContractJson.Options, Cancellation);

        Assert.Equal([("Children in the gym", PolicyStatus.Active), ("Pets", PolicyStatus.Draft)], listed?.Select(policy => (policy.Title, policy.Status)));
    }

    private async Task<(PropertyResponse Property, AmenityResponse Gym)> GymAsync()
    {
        PropertyResponse property = await api.CreatePropertyAsync(tenant, Cancellation);
        using HttpClient admin = api.Staff(tenant);
        using HttpResponseMessage response = await admin.PostAsJsonAsync("/api/v1/amenities", new { propertyId = property.Id.Value, name = "Gym", category = "Fitness" }, Cancellation);

        return (property, await StaffApi.CreatedAsync<AmenityResponse>(response, Cancellation));
    }

    private static async Task<PolicyResponse> CreateAsync(HttpClient admin, PropertyResponse property, AmenityResponse gym)
    {
        using HttpResponseMessage response = await admin.PostAsJsonAsync(
            Route,
            new
            {
                propertyId = property.Id.Value,
                type = "Children",
                title = "Children in the gym",
                content = "Guests under 16 must be accompanied by an adult.",
                appliesTo = new { kind = "Amenity", id = gym.Id },
            },
            Cancellation);
        PolicyResponse created = await StaffApi.CreatedAsync<PolicyResponse>(response, Cancellation);
        Assert.Equal(new ContractSubject(SubjectKind.Amenity, gym.Id), created.AppliesTo);

        return created;
    }
}
