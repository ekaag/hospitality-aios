using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Pos;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.Integrations.Mock.Pos;

/// <summary>
/// The outlets and menus the mock POS holds. Read when a property's connector is created; set them before the host
/// starts.
/// </summary>
public sealed class MockPosOptions
{
    /// <summary>
    /// Each property's outlets.
    /// </summary>
    public Dictionary<PropertyId, List<PosOutlet>> Outlets { get; } = [];

    /// <summary>
    /// Each outlet's menus, by outlet ID; outlet IDs are unique across the mock.
    /// </summary>
    public Dictionary<ExternalId, List<PosMenu>> Menus { get; } = [];
}
