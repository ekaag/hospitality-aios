using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationRuntime.Stays;

/// <summary>
/// Brings a property's in-house stays from its PMS, whichever provider is configured, into canonical guests and stays,
/// linking the provider's IDs through <c>ExternalReference</c> (P3, Journeys 9 and 18). Changes are staged; the caller
/// commits them. Scoped; current tenant only.
/// </summary>
/// <remarks>
/// A PMS stay already linked updates its canonical stay. An unlinked one is matched to the in-house stay in the same
/// unit with the same arrival, if there is one: a stay created natively, or imported from the property's previous PMS,
/// keeps its canonical ID and gains this provider's IDs. Otherwise a stay (and, if needed, a guest) is created and
/// checked in. Departures are not inferred from a stay's absence. Anything that cannot be applied is skipped, with a
/// reason, before it changes anything.
/// </remarks>
public interface IPmsStayImport
{
    /// <summary>
    /// Imports the PMS's in-house stays.
    /// </summary>
    /// <param name="propertyId">The property, in the current tenant.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    /// <returns>What was imported and skipped; <c>property.not_found</c>; or the resolver's error when the property has no
    /// usable PMS.</returns>
    Task<Result<StayImportReport>> ImportInHouseStaysAsync(PropertyId propertyId, CancellationToken cancellationToken);
}
