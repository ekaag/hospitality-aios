using HospitalityOS.Ai.Tracing;
using HospitalityOS.Contracts.Api.Ai;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.Ai.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static HospitalityOS.UnitTests.Ai.TestDoubles.AiTestHost;
using DomainOutcome = HospitalityOS.Domain.Capabilities.AITraces.AITraceOutcome;

namespace HospitalityOS.UnitTests.Ai.Tracing;

public sealed class AITraceReaderTests
{
    private const string Asked = "Is the pool open? I'm alex@example.com.";

    private static readonly TenantId tenant = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000000e0001"));
    private static readonly TenantId otherTenant = new(Guid.Parse("01890a5d-ac96-774b-bcce-0000000e0002"));
    private static readonly Guid flow = Guid.Parse("01890a5d-ac96-774b-bcce-0000000000ff");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(StaffRole.Manager, null, "Is the pool open? I'm [email].", true)]
    [InlineData(StaffRole.Admin, null, Asked, false)]
    [InlineData(StaffRole.Admin, "", "Is the pool open? I'm [email].", true)]
    [InlineData(StaffRole.Manager, "Admin, Manager", Asked, false)]
    public async Task Get_RedactsForRolesThatMayNotSeeTheTraceWhole(StaffRole role, string? unredacted, string expected, bool redacted)
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), collection =>
        {
            if (unredacted is not null)
            {
                collection.Configure<AITraceRedactionOptions>(options => options.UnredactedRoles = unredacted);
            }
        });
        await RecordAsync(services);

        AITraceResponse trace = (await ReadAsync(services, role, reader => reader.GetAsync(TraceId, Cancellation))).Value;

        Assert.Equal((expected, redacted), (trace.Body.TriggerText, trace.Redacted));
        Assert.Equal((TraceId, AITraceOutcome.Answered, flow, "GuestMessage"), (trace.Id, trace.Outcome, trace.CorrelationId, trace.TriggerType));
    }

    [Fact]
    public async Task Get_ListsTheEventsRecordedInTheTracesFlow_InTheReadersTenant()
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), _ => { });
        await RecordAsync(services);
        ListedEventStore store = services.GetRequiredService<ListedEventStore>();
        store.Events.AddRange(
        [
            Stored(1, tenant, flow, "GuestMessageReceived"),
            Stored(2, tenant, Guid.Parse("01890a5d-ac96-774b-bcce-0000000000aa"), "ContentItemCreated"),
            Stored(3, otherTenant, flow, "GuestMessageReceived"),
            Stored(4, tenant, flow, "AITraceCreated"),
        ]);

        AITraceResponse trace = (await ReadAsync(services, StaffRole.Manager, reader => reader.GetAsync(TraceId, Cancellation))).Value;

        Assert.Equal(["GuestMessageReceived", "AITraceCreated"], trace.FlowEvents.Select(flowEvent => flowEvent.EventType));
        Assert.Equal(AITraceResponse.MaxFlowEvents, Assert.Single(store.Queries).Limit);
    }

    [Fact]
    public async Task Get_AnUnknownOrEmptyId_IsNotFound()
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), _ => { });

        Result<AITraceResponse> unknown = await ReadAsync(services, StaffRole.Manager, reader => reader.GetAsync(Guid.CreateVersion7(), Cancellation));
        Result<AITraceResponse> empty = await ReadAsync(services, StaffRole.Manager, reader => reader.GetAsync(Guid.Empty, Cancellation));

        Assert.Equal((AITraceErrors.NotFoundCode, ErrorKind.NotFound), (unknown.Error.Code, unknown.Error.Kind));
        Assert.Equal(AITraceErrors.NotFoundCode, empty.Error.Code);
    }

    [Theory]
    [InlineData(0, null, "limit")]
    [InlineData(201, null, "limit")]
    [InlineData(null, "abc", "after")]
    [InlineData(null, "123.zz", "after")]
    [InlineData(null, "-5.01890a5dac96774bbcce000000000025", "after")]
    [InlineData(null, "99999999999999999999.01890a5dac96774bbcce000000000025", "after")]
    [InlineData(null, "638000000000000000.00000000000000000000000000000000", "after")]
    public async Task List_AnInvalidLimitOrCursor_IsAValidationError(int? limit, string? after, string field)
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), _ => { });

        Result<AITracePage> page = await ReadAsync(services, StaffRole.Manager, reader => reader.ListAsync(new AITraceListRequest(Limit: limit, After: after), Cancellation));

        Assert.Equal(field, Assert.Single(Assert.IsType<ValidationError>(page.Error).Fields).Field);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData(" Admin , Manager ", true)]
    [InlineData("", true)]
    [InlineData("Admin,Chef", false)]
    [InlineData("admin", false)]
    public void Options_UnredactedRolesMustAllBeStaffRoles(string roles, bool valid)
    {
        using ServiceProvider services = Build(new ScriptedModel(), new ExecutorScript(), collection =>
            collection.Configure<AITraceRedactionOptions>(options => options.UnredactedRoles = roles));

        Exception? failure = Record.Exception(() => services.GetRequiredService<IOptions<AITraceRedactionOptions>>().Value);

        if (valid)
        {
            Assert.Null(failure);
        }
        else
        {
            Assert.Contains("is not a staff role (Worker, Manager, Admin).", Assert.IsType<OptionsValidationException>(failure).Message, StringComparison.Ordinal);
        }
    }

    private static async Task RecordAsync(ServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);

        await scope.ServiceProvider.GetRequiredService<IAITraceRecorder>()
            .CompleteAsync(new AITraceCompletion(new AITraceTrigger(AITraceTrigger.GuestMessage, null, Asked), DomainOutcome.Answered, "Yes.", []), Cancellation);
    }

    private static async Task<T> ReadAsync<T>(ServiceProvider services, StaffRole role, Func<IAITraceReader, Task<T>> read)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(Actor.Staff("ana", role));

        return await read(scope.ServiceProvider.GetRequiredService<IAITraceReader>());
    }

    private static StoredEvent Stored(long position, TenantId tenantId, Guid correlation, string type) =>
        new(position, new EventMessage(Guid.CreateVersion7(), type, 1, tenantId, "Aggregate", Guid.CreateVersion7(), correlation, Now, "{}"));
}
