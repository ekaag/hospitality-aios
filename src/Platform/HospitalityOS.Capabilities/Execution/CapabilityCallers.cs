using HospitalityOS.Contracts.Capabilities;
using HospitalityOS.SharedKernel.Execution;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// Which <see cref="CapabilityCaller"/> an actor is. Staff are their role; staff without a known role are no caller,
/// so they may invoke nothing.
/// </summary>
internal static class CapabilityCallers
{
    public static CapabilityCaller? Of(Actor actor) => actor switch
    {
        { Type: ActorType.System } => CapabilityCaller.System,
        { Type: ActorType.Agent } => CapabilityCaller.Agent,
        { Type: ActorType.Guest } => CapabilityCaller.Guest,
        { Type: ActorType.Staff, Role: StaffRole.Worker } => CapabilityCaller.Worker,
        { Type: ActorType.Staff, Role: StaffRole.Manager } => CapabilityCaller.Manager,
        { Type: ActorType.Staff, Role: StaffRole.Admin } => CapabilityCaller.Admin,
        _ => null,
    };
}
