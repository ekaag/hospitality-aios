using System.Text.Json;
using HospitalityOS.Capabilities;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Organizations.Organizations;
using HospitalityOS.IntegrationTests.Capabilities.TestCapabilities;
using HospitalityOS.Ports.Auditing;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Capabilities;

/// <summary>
/// The capability pipeline in the real host: lookup, authorization, confirmation, idempotency, input and output
/// validation, atomic save with invocation record, events, and audit (P7, P8, spec §35).
/// </summary>
public sealed class CapabilityExecutorTests : IAsyncLifetime
{
    private static readonly Actor admin = Actor.Staff("ana", StaffRole.Admin);
    private static readonly Actor concierge = Actor.Agent("concierge");
    private static readonly Actor guest = new(ActorType.Guest, "guest-1");
    private static readonly Guid stayId = Guid.Parse("01890a5d-ac96-774b-bcce-000000000412");

    private readonly TenantId tenantId = new(Guid.CreateVersion7());
    private readonly WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Eventing:Dispatcher:Enabled", "false");
        builder.ConfigureTestServices(services => services
            .AddSingleton<CapabilityProbe>()
            .AddCapability<RenameOrganizationCapability>()
            .AddCapability<GetOrganizationCapability>()
            .AddCapability<NotifyGuestCapability>()
            .AddCapability<NullOutputCapability>());
    });

    public static TheoryData<Actor> ForbiddenCallers => new(
        guest,
        Actor.System,
        Actor.Staff("wes", StaffRole.Worker),
        Actor.Staff("max", StaffRole.Manager),
        new Actor(ActorType.Staff, "role-unknown"));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private CapabilityProbe Probe => factory.Services.GetRequiredService<CapabilityProbe>();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task Execute_ReadCapability_ReturnsOutputAndRecordsInvocationEventsAndAudit()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(guest, Get(organization), flow);

        CapabilityResponse response = result.Value;
        Assert.Equal(("Organization.Get", 1, false), (response.Name, response.Version, response.Replayed));
        Assert.Equal("Aurora Hotels", response.Output.GetProperty("name").GetString());
        CapabilityInvocation invocation = await LoadInvocationAsync(response.InvocationId);
        Assert.Equal(
            (CapabilityInvocationStatus.Succeeded, guest, flow, (string?)null, response.Output.GetRawText()),
            (invocation.Status, invocation.Actor, invocation.CorrelationId, invocation.IdempotencyKey, invocation.Output));
        Assert.Equal(["CapabilityInvocationRequested", "CapabilityInvocationSucceeded"], await EventTypesAsync(flow));
        AuditEntry audit = Assert.Single(await AuditAsync(flow));
        Assert.Equal(
            ("Organization.Get", AuditOutcome.Succeeded, "CapabilityInvocation", response.InvocationId.ToString()),
            (audit.Record.Action, audit.Record.Outcome, audit.Record.TargetType, audit.Record.TargetId));
        Assert.Contains($"input {{\"organizationId\":\"{organization}\"}}", audit.Record.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_StateChange_SavesTheChangeWithTheInvocationInOneFlow()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1"), flow);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("Aurora Group", (await LoadOrganizationAsync(organization)).Name);
        Assert.Equal(["CapabilityInvocationRequested", "CapabilityInvocationSucceeded", "OrganizationUpdated"], (await EventTypesAsync(flow)).Order(StringComparer.Ordinal));
        Assert.Equal("key-1", (await LoadInvocationAsync(result.Value.InvocationId)).IdempotencyKey);
    }

    [Theory]
    [InlineData("Organization.Unknown", null)]
    [InlineData("organization.get", null)]
    [InlineData("Organization.Get", 2)]
    [InlineData("", null)]
    public async Task Execute_UnknownCapability_IsRejectedAndAuditedAsDenied(string name, int? version)
    {
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(concierge, new CapabilityRequest(name, Json(new { }), version), flow);

        AssertError(result, CapabilityErrors.UnknownCode, ErrorKind.NotFound);
        AuditEntry audit = Assert.Single(await AuditAsync(flow));
        Assert.Equal((name.Length == 0 ? "capability.unknown" : name, AuditOutcome.Denied), (audit.Record.Action, audit.Record.Outcome));
        Assert.Empty(await EventTypesAsync(flow));
    }

    [Theory]
    [MemberData(nameof(ForbiddenCallers))]
    public async Task Execute_CallerNotAllowed_IsForbiddenWithoutRunning(Actor caller)
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(caller, Rename(organization, "Hijacked", "key-1"), flow);

        AssertError(result, CapabilityErrors.ForbiddenCode, ErrorKind.Forbidden);
        Assert.Equal(0, Probe.Runs(RenameOrganizationCapability.Name));
        Assert.Equal("Aurora Hotels", (await LoadOrganizationAsync(organization)).Name);
        Assert.Equal(AuditOutcome.Denied, Assert.Single(await AuditAsync(flow)).Record.Outcome);
    }

    [Fact]
    public async Task Execute_ConfirmationRequiredButNotGiven_IsRejectedWithoutRunning()
    {
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(concierge, Notify("key-1", confirmed: false), flow);

        AssertError(result, CapabilityErrors.ConfirmationRequiredCode, ErrorKind.InvalidState);
        Assert.Equal(0, Probe.Runs(NotifyGuestCapability.Name));
        Assert.Equal(AuditOutcome.Denied, Assert.Single(await AuditAsync(flow)).Record.Outcome);
    }

    [Fact]
    public async Task Execute_ConfirmationRequiredAndGiven_Runs()
    {
        Result<CapabilityResponse> result = await ExecuteAsync(concierge, Notify("key-1", confirmed: true));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Probe.Runs(NotifyGuestCapability.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-long")]
    public async Task Execute_KeyRequiredWithoutAUsableKey_IsRejectedWithoutRunning(string? key)
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        string? sent = key == "too-long" ? new string('k', CapabilityInvocation.MaxIdempotencyKeyLength + 1) : key;
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(admin, Rename(organization, "Aurora Group", sent), flow);

        AssertError(result, CapabilityErrors.IdempotencyKeyRequiredCode, ErrorKind.Validation);
        Assert.Equal(0, Probe.Runs(RenameOrganizationCapability.Name));
        Assert.Equal(AuditOutcome.Failed, Assert.Single(await AuditAsync(flow)).Record.Outcome);
    }

    [Theory]
    [InlineData("""{ "organizationId": "01890a5d-ac96-774b-bcce-000000000001" }""")]
    [InlineData("""{ "organizationId": "01890a5d-ac96-774b-bcce-000000000001", "name": "X", "failWith": null, "invented": true }""")]
    [InlineData("""{ "organizationId": "not-a-guid", "name": "X" }""")]
    [InlineData("""{ "organizationId": "01890a5d-ac96-774b-bcce-000000000001", "name": 42 }""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public async Task Execute_InputNotMatchingTheSchema_IsRejectedWithoutRunning(string input)
    {
        using JsonDocument document = JsonDocument.Parse(input);
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(
            admin,
            new CapabilityRequest(RenameOrganizationCapability.Name, document.RootElement.Clone(), IdempotencyKey: "key-1"),
            flow);

        AssertError(result, CapabilityErrors.InvalidInputCode, ErrorKind.Validation);
        Assert.Equal(0, Probe.Runs(RenameOrganizationCapability.Name));
        Assert.Equal(AuditOutcome.Failed, Assert.Single(await AuditAsync(flow)).Record.Outcome);
    }

    [Fact]
    public async Task Execute_NoInputAtAll_IsRejected()
    {
        Result<CapabilityResponse> result = await ExecuteAsync(concierge, new CapabilityRequest(GetOrganizationCapability.Name, default));

        AssertError(result, CapabilityErrors.InvalidInputCode, ErrorKind.Validation);
    }

    [Fact]
    public async Task Execute_SameKeyAndInputAgain_ReturnsTheFirstResultWithoutRunningAgain()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        Result<CapabilityResponse> first = await ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1"));
        Guid retryFlow = Guid.CreateVersion7();

        Result<CapabilityResponse> repeat = await ExecuteAsync(admin, Rename(organization, "Aurora Group", " key-1 "), retryFlow);

        Assert.Equal(
            (first.Value.InvocationId, first.Value.Output.GetRawText(), true),
            (repeat.Value.InvocationId, repeat.Value.Output.GetRawText(), repeat.Value.Replayed));
        Assert.Equal(1, Probe.Runs(RenameOrganizationCapability.Name));
        Assert.Equal(2, (await LoadOrganizationAsync(organization)).Version);
        Assert.Empty(await EventTypesAsync(retryFlow));
        AuditEntry audit = Assert.Single(await AuditAsync(retryFlow));
        Assert.Equal(AuditOutcome.Succeeded, audit.Record.Outcome);
        Assert.Contains($"replayed invocation {first.Value.InvocationId}", audit.Record.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Execute_SameKeyWithDifferentInput_IsAConflict()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        await ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1"));

        Result<CapabilityResponse> reused = await ExecuteAsync(admin, Rename(organization, "Other Name", "key-1"));

        AssertError(reused, CapabilityErrors.IdempotencyKeyReusedCode, ErrorKind.Conflict);
        Assert.Equal(("Aurora Group", 1), ((await LoadOrganizationAsync(organization)).Name, Probe.Runs(RenameOrganizationCapability.Name)));
    }

    [Fact]
    public async Task Execute_CapabilityFails_DiscardsItsChangesAndRecordsTheFailure()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        Guid flow = Guid.CreateVersion7();

        Result<CapabilityResponse> result = await ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1", failWith: "organization.locked"), flow);

        AssertError(result, "organization.locked", ErrorKind.InvalidState);
        Organization unchanged = await LoadOrganizationAsync(organization);
        Assert.Equal(("Aurora Hotels", 1), (unchanged.Name, unchanged.Version));
        Assert.Equal(["CapabilityInvocationRequested", "CapabilityInvocationFailed"], await EventTypesAsync(flow));
        AuditEntry audit = Assert.Single(await AuditAsync(flow));
        CapabilityInvocation invocation = await LoadInvocationAsync(Guid.Parse(audit.Record.TargetId!));
        Assert.Equal((CapabilityInvocationStatus.Failed, "organization.locked", AuditOutcome.Failed), (invocation.Status, invocation.ErrorCode, audit.Record.Outcome));
    }

    [Fact]
    public async Task Execute_AfterAFailure_TheSameKeyCanBeRetried()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        await ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1", failWith: "organization.locked"));

        Result<CapabilityResponse> retry = await ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1"));

        Assert.False(retry.Value.Replayed);
        Assert.Equal("Aurora Group", (await LoadOrganizationAsync(organization)).Name);
    }

    [Fact]
    public async Task Execute_CapabilityThrows_RecordsTheCrashAndRethrows()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        Guid flow = Guid.CreateVersion7();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1", failWith: "crash"), flow));

        Assert.Equal("Aurora Hotels", (await LoadOrganizationAsync(organization)).Name);
        AuditEntry audit = Assert.Single(await AuditAsync(flow));
        Assert.Equal(
            (AuditOutcome.Failed, CapabilityErrors.CrashedCode),
            (audit.Record.Outcome, (await LoadInvocationAsync(Guid.Parse(audit.Record.TargetId!))).ErrorCode));
    }

    [Fact]
    public async Task Execute_OutputBreakingItsSchema_IsRecordedAsAFailure()
    {
        Result<CapabilityResponse> result = await ExecuteAsync(Actor.System, new CapabilityRequest(NullOutputCapability.Name, Json(new { note = "x" })));

        AssertError(result, CapabilityErrors.InvalidOutputCode, ErrorKind.InvalidState);
    }

    [Fact]
    public async Task Execute_SensitiveCapability_KeepsInputOutOfTheAudit()
    {
        Guid flow = Guid.CreateVersion7();

        await ExecuteAsync(concierge, Notify("key-1", confirmed: true, text: "Room 412 is ready, Ms Patel"), flow);

        Assert.Equal("v1", Assert.Single(await AuditAsync(flow)).Record.Detail);
    }

    [Fact]
    public async Task Execute_TwoRacingInvocationsWithOneKey_RunTheEffectOnceAndShareTheResult()
    {
        Probe.HoldUntil(2);

        Result<CapabilityResponse>[] results = await Task.WhenAll(
            ExecuteAsync(concierge, Notify("key-1", confirmed: true)),
            ExecuteAsync(concierge, Notify("key-1", confirmed: true)));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null));
        Assert.Equal(results[0].Value.InvocationId, results[1].Value.InvocationId);
        Assert.Equal(results[0].Value.Output.GetRawText(), results[1].Value.Output.GetRawText());
        Assert.Single(results, result => result.Value.Replayed);
    }

    [Fact]
    public async Task Execute_TwoRacingChangesToOneAggregate_LoserConflictsAndItsRetryReplaysTheWinner()
    {
        OrganizationId organization = await CreateOrganizationAsync("Aurora Hotels");
        Probe.HoldUntil(2);

        Result<CapabilityResponse>[] results = await Task.WhenAll(
            ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1")),
            ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1")));
        Probe.HoldUntil(0);
        Result<CapabilityResponse> loser = Assert.Single(results, result => result.IsFailure);
        Result<CapabilityResponse> winner = Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, loser.Error.Kind);

        Result<CapabilityResponse> retry = await ExecuteAsync(admin, Rename(organization, "Aurora Group", "key-1"));

        Assert.Equal((winner.Value.InvocationId, true), (retry.Value.InvocationId, retry.Value.Replayed));
        Assert.Equal(2, (await LoadOrganizationAsync(organization)).Version);
    }

    [Fact]
    public async Task Execute_RunsInItsOwnScope_LeavingTheCallersStagedWorkUncommitted()
    {
        OrganizationId staged = new(Guid.CreateVersion7());
        await using AsyncServiceScope caller = OpenScope(concierge);
        Organization organization = Organization.Create(staged, tenantId, "Staged, never committed", DateTimeOffset.UtcNow).Value;
        await caller.ServiceProvider.GetRequiredService<IOrganizationRepository>().AddAsync(organization, Cancellation);

        await caller.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(Notify("key-1", confirmed: true), Cancellation);

        await using AsyncServiceScope reader = OpenScope(concierge);
        Assert.Null(await reader.ServiceProvider.GetRequiredService<IOrganizationRepository>().GetByIdAsync(staged, Cancellation));
    }

    private static CapabilityRequest Rename(OrganizationId organization, string name, string? key, string? failWith = null) =>
        new(RenameOrganizationCapability.Name, Json(new { organizationId = organization.Value, name, failWith }), IdempotencyKey: key);

    private static CapabilityRequest Get(OrganizationId organization) =>
        new(GetOrganizationCapability.Name, Json(new { organizationId = organization.Value }));

    private static CapabilityRequest Notify(string key, bool confirmed, string text = "Your towels are on the way.") =>
        new(NotifyGuestCapability.Name, Json(new { stayId, text }), IdempotencyKey: key, Confirmed: confirmed);

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, ContractJson.Options);

    private static void AssertError(Result<CapabilityResponse> result, string code, ErrorKind kind)
    {
        Assert.True(result.IsFailure, "Expected a failure.");
        Assert.Equal((code, kind), (result.Error.Code, result.Error.Kind));
    }

    private AsyncServiceScope OpenScope(Actor actor, Guid? flow = null)
    {
        AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        OperationContext operation = scope.ServiceProvider.GetRequiredService<OperationContext>();
        operation.JoinFlow(flow ?? Guid.CreateVersion7(), causation: null);
        operation.ActAs(actor);

        return scope;
    }

    private async Task<Result<CapabilityResponse>> ExecuteAsync(Actor actor, CapabilityRequest request, Guid? flow = null)
    {
        await using AsyncServiceScope scope = OpenScope(actor, flow);

        return await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(request, Cancellation);
    }

    private async Task<OrganizationId> CreateOrganizationAsync(string name)
    {
        OrganizationId id = new(Guid.CreateVersion7());
        await using AsyncServiceScope scope = OpenScope(Actor.System);
        await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().AddAsync(Organization.Create(id, tenantId, name, DateTimeOffset.UtcNow).Value, Cancellation);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation)).IsSuccess);

        return id;
    }

    private async Task<Organization> LoadOrganizationAsync(OrganizationId id)
    {
        await using AsyncServiceScope scope = OpenScope(Actor.System);
        Organization? organization = await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>().GetByIdAsync(id, Cancellation);

        return Assert.IsType<Organization>(organization);
    }

    private async Task<CapabilityInvocation> LoadInvocationAsync(Guid id)
    {
        await using AsyncServiceScope scope = OpenScope(Actor.System);
        CapabilityInvocation? invocation = await scope.ServiceProvider.GetRequiredService<ICapabilityInvocationRepository>().GetByIdAsync(new CapabilityInvocationId(id), Cancellation);

        return Assert.IsType<CapabilityInvocation>(invocation);
    }

    private async Task<string[]> EventTypesAsync(Guid flow)
    {
        await using AsyncServiceScope scope = OpenScope(Actor.System);
        IReadOnlyList<StoredEvent> events = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .ReadAsync(new EventStoreQuery(TenantId: tenantId, CorrelationId: flow), Cancellation);

        return [.. events.Select(stored => stored.Message.EventType)];
    }

    private async Task<IReadOnlyList<AuditEntry>> AuditAsync(Guid flow)
    {
        await using AsyncServiceScope scope = OpenScope(Actor.System);

        return await scope.ServiceProvider.GetRequiredService<IAuditLog>().ReadAsync(new AuditQuery(CorrelationId: flow), Cancellation);
    }
}
