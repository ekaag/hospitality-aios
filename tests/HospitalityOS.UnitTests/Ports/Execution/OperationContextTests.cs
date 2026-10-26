using HospitalityOS.Ports.Execution;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.UnitTests.Eventing.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Ports.Execution;

public sealed class OperationContextTests
{
    private static readonly Guid generated = Guid.Parse("01890a5d-ac96-774b-bcce-000000000001");
    private static readonly Guid callerFlow = Guid.Parse("01890a5d-ac96-774b-bcce-000000000002");
    private static readonly Guid causeId = Guid.Parse("01890a5d-ac96-774b-bcce-000000000003");

    [Fact]
    public void CorrelationId_NobodyJoinedAFlow_StartsOneFlowForTheWholeOperation()
    {
        OperationContext operation = new(new FixedIdGenerator(generated));

        Assert.Equal(generated, operation.CorrelationId);
        Assert.Equal(generated, operation.CorrelationId);
        Assert.Null(operation.CausationId);
        Assert.Equal(Actor.System, operation.Actor);
    }

    [Fact]
    public void JoinFlow_BeforeFirstRead_UsesTheCallersFlowAndCause()
    {
        OperationContext operation = new(new FixedIdGenerator(generated));

        operation.JoinFlow(callerFlow, causeId);

        Assert.Equal((callerFlow, (Guid?)causeId), (operation.CorrelationId, operation.CausationId));
    }

    [Fact]
    public void JoinFlow_AfterTheOperationStartedAnotherFlow_Throws()
    {
        OperationContext operation = new(new FixedIdGenerator(generated));
        _ = operation.CorrelationId;

        Assert.Throws<InvalidOperationException>(() => operation.JoinFlow(callerFlow, null));
    }

    [Fact]
    public void JoinFlow_EmptyCorrelation_Throws()
    {
        OperationContext operation = new(new FixedIdGenerator(generated));

        Assert.Throws<ArgumentException>(() => operation.JoinFlow(Guid.Empty, null));
    }

    [Fact]
    public void ActAs_SameActorAgain_IsAllowedButSwitchingActorsThrows()
    {
        OperationContext operation = new(new FixedIdGenerator(generated));
        Actor maria = new(ActorType.Staff, "maria");

        operation.ActAs(maria);
        operation.ActAs(maria);

        Assert.Equal(maria, operation.Actor);
        Assert.Throws<InvalidOperationException>(() => operation.ActAs(Actor.Agent("GuestServiceController")));
    }
}
