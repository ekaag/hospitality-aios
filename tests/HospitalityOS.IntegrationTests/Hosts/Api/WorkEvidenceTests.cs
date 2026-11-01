using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Api.Work;
using HospitalityOS.Contracts.Capabilities.GuestService;
using HospitalityOS.Contracts.Capabilities.HumanActions;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.Workflows.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Seed = HospitalityOS.Seeding.AuroraGrand;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Evidence for work (Journey 14, K3) against the seeded Aurora Grand: the worker holding accepted work uploads a photo,
/// completes the work with its key, and whoever may see the work reads it back; nobody else may upload, only photos and
/// PDFs within the size limit are kept, and completion refuses keys that are not files uploaded for that work, in that
/// tenant.
/// </summary>
public sealed class WorkEvidenceTests : IAsyncLifetime
{
    private static readonly Actor agent = Actor.Agent("guestservice.extract");
    private static readonly byte[] photo = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01];

    private readonly StaffApi api = new(seedAuroraGrand: true, dispatchEvents: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        await api.HandledAsync(Seed.TenantId, "StaffMemberCreated", DutyRerouting.JoinedName, Cancellation);

    public ValueTask DisposeAsync() => api.DisposeAsync();

    [Fact]
    public async Task AWorker_CompletesWithAPhoto_WhichTheyAndManagersCanRead_AndOthersCannot()
    {
        WorkItemResponse work = await TowelsForJoAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        using HttpClient sam = Worker(Seed.SamPatel);
        using HttpClient manager = api.Staff(Seed.TenantId, "Manager");
        await StepAsync(jo, work, "accept");

        using HttpResponseMessage uploaded = await UploadAsync(jo, work.Id, photo, "image/jpeg");
        EvidenceResponse file = await StaffApi.CreatedAsync<EvidenceResponse>(uploaded, Cancellation);
        await StepAsync(jo, work, "start");
        WorkItemResponse completed = await StepAsync(jo, work, "complete", new CompleteWorkRequest("Left at the door", [file.Key]));

        Assert.Equal(("image/jpeg", photo.LongLength), (file.ContentType, file.Length));
        Assert.StartsWith($"{work.Id:N}/", file.Key, StringComparison.Ordinal);
        Assert.Equal($"/api/v1/work/{work.Id}/evidence/{file.Key.Split('/')[1]}", uploaded.Headers.Location?.OriginalString);
        Assert.Equal([file.Key], completed.Evidence);

        foreach (HttpClient reader in new[] { jo, manager })
        {
            using HttpResponseMessage read = await reader.GetAsync(uploaded.Headers.Location, Cancellation);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal("image/jpeg", read.Content.Headers.ContentType?.MediaType);
            Assert.Equal(photo, await read.Content.ReadAsByteArrayAsync(Cancellation));
        }

        using HttpResponseMessage notTheirs = await sam.GetAsync(uploaded.Headers.Location, Cancellation);
        using HttpResponseMessage noSuchFile = await manager.GetAsync($"/api/v1/work/{work.Id}/evidence/{Guid.NewGuid():N}", Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        Assert.Equal((HttpStatusCode.NotFound, "work.evidence_not_found"), await StaffApi.ProblemAsync(noSuchFile, Cancellation));
    }

    [Fact]
    public async Task Evidence_IsOnlyForTheWorkersAcceptedWork()
    {
        WorkItemResponse work = await TowelsForJoAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        using HttpClient sam = Worker(Seed.SamPatel);
        using HttpClient manager = api.Staff(Seed.TenantId, "Manager");

        using HttpResponseMessage beforeAccepting = await UploadAsync(jo, work.Id, photo, "image/jpeg");
        await StepAsync(jo, work, "accept");
        using HttpResponseMessage someoneElse = await UploadAsync(sam, work.Id, photo, "image/jpeg");
        using HttpResponseMessage aManager = await UploadAsync(manager, work.Id, photo, "image/jpeg");

        Assert.Equal((HttpStatusCode.Forbidden, "work.evidence_not_allowed"), await StaffApi.ProblemAsync(beforeAccepting, Cancellation));
        Assert.Equal(HttpStatusCode.NotFound, someoneElse.StatusCode);
        Assert.Equal((HttpStatusCode.Forbidden, "work.evidence_not_allowed"), await StaffApi.ProblemAsync(aManager, Cancellation));
    }

    [Fact]
    public async Task OnlyPhotosAndPdfsWithinTheLimit_AreKept()
    {
        WorkItemResponse work = await TowelsForJoAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        await StepAsync(jo, work, "accept");

        using HttpResponseMessage text = await UploadAsync(jo, work.Id, "hello"u8.ToArray(), "text/plain");
        using HttpResponseMessage empty = await UploadAsync(jo, work.Id, [], "image/png");
        using HttpResponseMessage tooLarge = await UploadAsync(jo, work.Id, new byte[10 * 1024 * 1024 + 1], "image/png");
        using HttpResponseMessage pdf = await UploadAsync(jo, work.Id, "%PDF-1.7"u8.ToArray(), "application/pdf");

        Assert.Equal(["evidence.unsupported_type"], await StaffApi.FieldCodesAsync(text, Cancellation));
        Assert.Equal(["evidence.empty"], await StaffApi.FieldCodesAsync(empty, Cancellation));
        Assert.Equal(["evidence.too_large"], await StaffApi.FieldCodesAsync(tooLarge, Cancellation));
        Assert.Equal(HttpStatusCode.Created, pdf.StatusCode);
    }

    [Fact]
    public async Task Completing_WithKeysThatAreNotThisWorksFiles_IsRefused()
    {
        WorkItemResponse work = await TowelsForJoAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        await StepAsync(jo, work, "accept");
        EvidenceResponse file = await StaffApi.CreatedAsync<EvidenceResponse>(await UploadAsync(jo, work.Id, photo, "image/jpeg"), Cancellation);
        await StepAsync(jo, work, "start");
        string otherWorks = $"{Guid.NewGuid():N}/{file.Key.Split('/')[1]}";
        string neverUploaded = $"{work.Id:N}/{Guid.NewGuid():N}";

        using HttpResponseMessage refused = await jo.PostAsJsonAsync(
            $"/api/v1/work/{work.Id}/complete", new CompleteWorkRequest(null, [file.Key, otherWorks, "photo.jpg", neverUploaded]), ContractJson.Options, Cancellation);
        WorkItemResponse still = await StaffApi.OkAsync<WorkItemResponse>(await jo.GetAsync($"/api/v1/work/{work.Id}", Cancellation), Cancellation);

        Assert.Equal(
            ["human_action.evidence_not_found", "human_action.evidence_not_found", "human_action.evidence_not_found"],
            await StaffApi.FieldCodesAsync(refused, Cancellation));
        Assert.Equal(ActionStatus.InProgress, still.Status);
    }

    [Fact]
    public async Task AnotherTenantsFile_IsNoFileHere()
    {
        WorkItemResponse work = await TowelsForJoAsync();
        using HttpClient jo = Worker(Seed.JoOkafor);
        await StepAsync(jo, work, "accept");
        EvidenceResponse file = await StaffApi.CreatedAsync<EvidenceResponse>(await UploadAsync(jo, work.Id, photo, "image/jpeg"), Cancellation);

        await using AsyncServiceScope elsewhere = api.Factory.Services.CreateAsyncScope();
        elsewhere.ServiceProvider.GetRequiredService<TenantContext>().Set(new TenantId(Guid.CreateVersion7()));
        Application.Evidence.HumanActionEvidence evidence = elsewhere.ServiceProvider.GetRequiredService<Application.Evidence.HumanActionEvidence>();

        Assert.Equal([file.Key], await evidence.MissingAsync(work.Id, [file.Key], Cancellation));
        Assert.Null(await evidence.OpenAsync(work.Id, file.Key, Cancellation));
    }

    private HttpClient Worker(StaffMemberId id) => api.Staff(Seed.TenantId, "Worker", id.ToString());

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid work, byte[] bytes, string contentType)
    {
        using MultipartFormDataContent form = new();
        ByteArrayContent file = new(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", "evidence");

        return await client.PostAsync($"/api/v1/work/{work}/evidence", form, Cancellation);
    }

    private static async Task<WorkItemResponse> StepAsync(HttpClient client, WorkItemResponse work, string step, object? body = null)
    {
        using HttpResponseMessage response = body is null
            ? await client.PostAsync($"/api/v1/work/{work.Id}/{step}", null, Cancellation)
            : await client.PostAsJsonAsync($"/api/v1/work/{work.Id}/{step}", body, ContractJson.Options, Cancellation);

        return await StaffApi.OkAsync<WorkItemResponse>(response, Cancellation);
    }

    /// <summary>
    /// Alex's two towels, as the Guest Service Controller asks for them, once the router has given them to Jo.
    /// </summary>
    private async Task<WorkItemResponse> TowelsForJoAsync()
    {
        await using (AsyncServiceScope scope = api.Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(Seed.TenantId);
            scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(agent);
            Result<CapabilityResponse> created = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
                new CapabilityRequest(
                    "GuestService.CreateRequest",
                    JsonSerializer.SerializeToElement(new CreateRequestInput(Seed.AlexStay, "housekeeping.amenities", "bath_towel", 2), ContractJson.Options),
                    IdempotencyKey: "request-1"),
                Cancellation);
            Assert.True(created.IsSuccess, created.IsFailure ? $"{created.Error.Code}: {created.Error.Message}" : null);
        }

        using HttpClient jo = Worker(Seed.JoOkafor);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            IReadOnlyList<WorkItemResponse> items = await StaffApi.OkAsync<IReadOnlyList<WorkItemResponse>>(
                await jo.GetAsync($"/api/v1/work?propertyId={Seed.PropertyId}&view={WorkView.Assigned}", Cancellation), Cancellation);

            if (items.Count == 1)
            {
                return items[0];
            }

            api.Clock.Advance(TimeSpan.FromMilliseconds(50));
            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
