using HospitalityOS.Domain.Capabilities.AITraces;
using HospitalityOS.Domain.Capabilities.AITraces.Events;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Capabilities;

public sealed class AITraceTests
{
    private const string Body = """{"triggerText":"When does the pool close?"}""";

    private static readonly AITraceId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000002"));
    private static readonly PropertyId auroraGrand = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000003"));
    private static readonly Guid flow = Guid.Parse("01890a5d-ac96-774b-bcce-000000000004");
    private static readonly Guid cause = Guid.Parse("01890a5d-ac96-774b-bcce-000000000005");
    private static readonly Guid message = Guid.Parse("01890a5d-ac96-774b-bcce-000000000006");
    private static readonly DateTimeOffset startedAt = new(2026, 10, 28, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset completedAt = startedAt.AddSeconds(3);
    private static readonly Actor concierge = Actor.Agent("concierge.answer");

    [Fact]
    public void Record_KeepsTheWholeTurn_AndRaisesCreated()
    {
        AITrace trace = Record();

        Assert.Equal(
            (tenantId, (PropertyId?)auroraGrand, concierge, flow, (Guid?)cause, "GuestMessage", (Guid?)message, "question", AITraceOutcome.Answered, startedAt, completedAt, Body),
            (trace.TenantId, trace.PropertyId, trace.Actor, trace.CorrelationId, trace.CausationId, trace.TriggerType, trace.TriggerId, trace.Intent,
                trace.Outcome, trace.StartedAt, trace.CompletedAt, trace.Body));
        Assert.Equal(
            [new AITraceCreated(id, auroraGrand, "concierge.answer", "GuestMessage", message, AITraceOutcome.Answered, completedAt)],
            trace.DomainEvents);
        Assert.Equal(1, trace.Version);
    }

    [Fact]
    public void Record_WithoutAPropertyIntentOrTriggerId_IsAllowed()
    {
        AITrace trace = AITrace.Record(
            id, tenantId, null, Actor.System, flow, null, "ContentItemCreated", null, null, AITraceOutcome.Failed, startedAt, startedAt, Body);

        Assert.Equal(((PropertyId?)null, (string?)null, (Guid?)null, (string?)null), (trace.PropertyId, trace.ActorId, trace.TriggerId, trace.Intent));
    }

    [Theory]
    [InlineData("trigger", " GuestMessage")]
    [InlineData("trigger", "")]
    [InlineData("trigger", "long")]
    [InlineData("intent", "question ")]
    [InlineData("intent", "long")]
    [InlineData("intent", " ")]
    [InlineData("body", " ")]
    [InlineData("completed", "before started")]
    [InlineData("outcome", "undefined")]
    [InlineData("actor", "long")]
    public void Record_InvalidValues_Throw(string field, string value)
    {
        string trigger = field == "trigger" ? (value == "long" ? new string('t', AITrace.MaxTriggerTypeLength + 1) : value) : "GuestMessage";
        string intent = field == "intent" ? (value == "long" ? new string('i', AITrace.MaxIntentLength + 1) : value) : "question";
        string body = field == "body" ? value : Body;
        DateTimeOffset completed = field == "completed" ? startedAt.AddTicks(-1) : completedAt;
        AITraceOutcome outcome = field == "outcome" ? (AITraceOutcome)99 : AITraceOutcome.Answered;
        Actor actor = field == "actor" ? Actor.Agent(new string('a', AITrace.MaxActorIdLength + 1)) : concierge;

        Assert.ThrowsAny<ArgumentException>(() => AITrace.Record(id, tenantId, auroraGrand, actor, flow, cause, trigger, message, intent, outcome, startedAt, completed, body));
    }

    private static AITrace Record() =>
        AITrace.Record(id, tenantId, auroraGrand, concierge, flow, cause, "GuestMessage", message, "question", AITraceOutcome.Answered, startedAt, completedAt, Body);
}
