using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Execution;
using Xunit;

namespace HospitalityOS.UnitTests.Capabilities.Execution;

public sealed class CapabilityCallersTests
{
    public static TheoryData<Actor, CapabilityCaller?> Cases => new()
    {
        { Actor.System, CapabilityCaller.System },
        { Actor.Agent("concierge"), CapabilityCaller.Agent },
        { new Actor(ActorType.Guest, "guest-1"), CapabilityCaller.Guest },
        { Actor.Staff("wes", StaffRole.Worker), CapabilityCaller.Worker },
        { Actor.Staff("max", StaffRole.Manager), CapabilityCaller.Manager },
        { Actor.Staff("ana", StaffRole.Admin), CapabilityCaller.Admin },
        { new Actor(ActorType.Staff, "unknown-role"), null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Of_MapsTheActorToItsCaller(Actor actor, CapabilityCaller? expected)
    {
        Assert.Equal(expected, CapabilityCallers.Of(actor));
    }
}
