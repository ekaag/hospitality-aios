using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.Workflows.StaffSurface;

/// <summary>
/// The realtime channel a tenant's staff hear (P10: one per tenant), on which <c>WorkChanged</c> messages arrive.
/// </summary>
public static class StaffChannel
{
    public static string For(TenantId tenant) => $"tenant:{tenant.Value}:staff";
}
