using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationContracts.Pms;

/// <summary>
/// Reads stays from a property management system (spec §10), so the platform knows who is in house and in which unit
/// (Journeys 9–12). Read-only in Phase 1: the PMS stays the system of record for reservations, and check-in, checkout
/// and booking are out of scope. Initial implementation: <c>MockPms</c> (E3).
/// </summary>
/// <remarks>
/// An instance serves one property: obtain it from the integration runtime's per-property resolver (E2). Business code
/// never references an implementation (spec §10). IDs in results are the provider's own; the runtime maps them to
/// canonical IDs through <c>ExternalReference</c> (P3). Expected failures are returned as <see cref="IntegrationErrors"/>;
/// provider outages are thrown.
/// </remarks>
public interface IPmsConnector
{
    /// <summary>
    /// Gets one stay by the provider's reservation ID.
    /// </summary>
    /// <param name="stayId">The provider's ID for the stay.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The stay, or <see cref="IntegrationErrors.NotFound"/>.</returns>
    Task<Result<PmsStay>> GetStayAsync(ExternalId stayId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the stays currently in house, in no particular order.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The in-house stays; empty when nobody is staying.</returns>
    Task<IReadOnlyList<PmsStay>> ListInHouseStaysAsync(CancellationToken cancellationToken);
}
