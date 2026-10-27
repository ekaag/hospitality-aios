using HospitalityOS.IntegrationContracts;

namespace HospitalityOS.IntegrationRuntime.Stays;

/// <summary>
/// A PMS stay the import left alone.
/// </summary>
/// <param name="PmsStayId">The provider's ID for the stay.</param>
/// <param name="Reason">Why, for an administrator; free of personal data.</param>
public sealed record SkippedStay(ExternalId PmsStayId, string Reason);
