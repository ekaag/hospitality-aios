using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Workflows.GuestSurface;

/// <summary>
/// The realtime channel a guest hears (P10: one per stay, within its tenant), on which <c>StayChanged</c> messages arrive.
/// A stay token names the stay, so every conversation of the stay is heard, including ones started after connecting.
/// </summary>
public static class GuestChannel
{
    public static string For(TenantId tenant, StayContextId stay) => $"tenant:{tenant.Value}:stay:{stay.Value}";
}
