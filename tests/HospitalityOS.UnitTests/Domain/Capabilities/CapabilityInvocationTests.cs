using HospitalityOS.Domain.Capabilities.CapabilityInvocations;
using HospitalityOS.Domain.Capabilities.CapabilityInvocations.Events;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Capabilities;

public sealed class CapabilityInvocationTests
{
    private static readonly CapabilityInvocationId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000001"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-000000000002"));
    private static readonly Guid flow = Guid.Parse("01890a5d-ac96-774b-bcce-000000000003");
    private static readonly Guid cause = Guid.Parse("01890a5d-ac96-774b-bcce-000000000004");
    private static readonly DateTimeOffset requestedAt = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset completedAt = requestedAt.AddSeconds(2);
    private static readonly string hash = new('a', CapabilityInvocation.InputHashLength);
    private static readonly Actor worker = Actor.Staff("maria", StaffRole.Worker);

    [Fact]
    public void Request_RecordsWhoWhatAndFlow_AndRaisesRequested()
    {
        CapabilityInvocation invocation = Request("key-1");

        Assert.Equal(
            (tenantId, "HumanAction.Complete", 1, "key-1", hash, worker, flow, (Guid?)cause, CapabilityInvocationStatus.Requested, requestedAt),
            (invocation.TenantId, invocation.CapabilityName, invocation.CapabilityVersion, invocation.IdempotencyKey, invocation.InputHash,
                invocation.Actor, invocation.CorrelationId, invocation.CausationId, invocation.Status, invocation.RequestedAt));
        Assert.Equal([new CapabilityInvocationRequested(id, "HumanAction.Complete", 1, requestedAt)], invocation.DomainEvents);
        Assert.Equal(1, invocation.Version);
    }

    [Fact]
    public void Succeed_KeepsTheOutput_AndRaisesSucceeded()
    {
        CapabilityInvocation invocation = Request();

        invocation.Succeed("""{"done":true}""", completedAt);

        Assert.Equal((CapabilityInvocationStatus.Succeeded, """{"done":true}""", (DateTimeOffset?)completedAt), (invocation.Status, invocation.Output, invocation.CompletedAt));
        Assert.Equal(new CapabilityInvocationSucceeded(id, "HumanAction.Complete", 1, completedAt), invocation.DomainEvents[^1]);
    }

    [Fact]
    public void Fail_KeepsTheError_AndRaisesFailed()
    {
        CapabilityInvocation invocation = Request();

        invocation.Fail(Error.InvalidState("humanaction.not_assigned", "Not assigned."), completedAt);

        Assert.Equal(
            (CapabilityInvocationStatus.Failed, "humanaction.not_assigned", "Not assigned.", (string?)null),
            (invocation.Status, invocation.ErrorCode, invocation.ErrorMessage, invocation.Output));
        Assert.Equal(new CapabilityInvocationFailed(id, "HumanAction.Complete", 1, "humanaction.not_assigned", completedAt), invocation.DomainEvents[^1]);
    }

    [Fact]
    public void Fail_OverlongMessage_IsTruncated()
    {
        CapabilityInvocation invocation = Request();

        invocation.Fail(Error.InvalidState("x.y", new string('m', CapabilityInvocation.MaxErrorMessageLength + 5)), completedAt);

        Assert.Equal(CapabilityInvocation.MaxErrorMessageLength, invocation.ErrorMessage!.Length);
    }

    [Fact]
    public void Complete_Twice_Throws()
    {
        CapabilityInvocation succeeded = Request();
        succeeded.Succeed("{}", completedAt);
        CapabilityInvocation failed = Request();
        failed.Fail(Error.InvalidState("x.y", "No."), completedAt);

        Assert.Throws<InvalidOperationException>(() => succeeded.Succeed("{}", completedAt));
        Assert.Throws<InvalidOperationException>(() => succeeded.Fail(Error.InvalidState("x.y", "No."), completedAt));
        Assert.Throws<InvalidOperationException>(() => failed.Succeed("{}", completedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" key")]
    [InlineData("key ")]
    public void Request_UntrimmedOrEmptyKey_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => Request(key));
    }

    [Fact]
    public void Request_WrongHashLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CapabilityInvocation.Request(id, tenantId, "HumanAction.Complete", 1, null, "abc", worker, flow, null, requestedAt));
    }

    private static CapabilityInvocation Request(string? key = null) =>
        CapabilityInvocation.Request(id, tenantId, "HumanAction.Complete", 1, key, hash, worker, flow, cause, requestedAt);
}
