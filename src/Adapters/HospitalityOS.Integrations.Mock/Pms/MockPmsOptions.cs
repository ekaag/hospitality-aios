using HospitalityOS.IntegrationContracts.Pms;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Integrations.Mock.Pms;

/// <summary>
/// The reservations the mock PMS holds, per property. Read when a property's connector is created; set them before the
/// host starts.
/// </summary>
public sealed class MockPmsOptions
{
    /// <summary>
    /// Each property's stays, as the PMS would report them.
    /// </summary>
    public Dictionary<PropertyId, List<PmsStay>> Stays { get; } = [];
}
