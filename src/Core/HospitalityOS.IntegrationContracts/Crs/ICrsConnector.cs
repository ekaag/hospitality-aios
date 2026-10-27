namespace HospitalityOS.IntegrationContracts.Crs;

/// <summary>
/// Placeholder for a central reservation system (spec §10): defined so the boundary exists, with no Phase 1
/// implementation. Reservations and OTA integrations are out of scope, so an agent must never imply that a booking was
/// made.
/// </summary>
/// <remarks>
/// An instance serves one property: obtain it from the integration runtime's per-property resolver (E2). Business code
/// never references an implementation (spec §10). IDs in results are the provider's own; the runtime maps them to
/// canonical IDs through <c>ExternalReference</c> (P3). Expected failures are returned as <see cref="IntegrationErrors"/>;
/// provider outages are thrown.
/// </remarks>
public interface ICrsConnector
{
    /// <summary>
    /// Lists the accommodation types bookable for every night of a stay, in no particular order.
    /// </summary>
    /// <param name="arrival">Property-local arrival date.</param>
    /// <param name="departure">Property-local departure date; after arrival.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The available accommodation types; empty when nothing is available.</returns>
    Task<IReadOnlyList<RoomAvailability>> GetAvailabilityAsync(DateOnly arrival, DateOnly departure, CancellationToken cancellationToken);
}
