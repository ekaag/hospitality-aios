using HospitalityOS.IntegrationContracts;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.IntegrationRuntime.Stays;

/// <summary>
/// A PMS stay applied to a canonical stay.
/// </summary>
/// <param name="PmsStayId">The provider's ID for the stay.</param>
/// <param name="StayContextId">The canonical stay.</param>
/// <param name="GuestId">The canonical guest.</param>
/// <param name="Outcome">What the import did.</param>
public sealed record ImportedStay(ExternalId PmsStayId, StayContextId StayContextId, GuestId GuestId, StayImportOutcome Outcome);
