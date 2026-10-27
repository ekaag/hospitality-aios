using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationContracts.Pos;

/// <summary>
/// Reads outlets and menus, with prices and availability, from a point-of-sale system (spec §10). Read-only in Phase 1:
/// ordering and settlement are out of scope. Dietary attributes are not POS data; the platform owns them (Journey 3).
/// Initial implementation: <c>MockPos</c> (E3).
/// </summary>
/// <remarks>
/// An instance serves one property: obtain it from the integration runtime's per-property resolver (E2). Business code
/// never references an implementation (spec §10). IDs in results are the provider's own; the runtime maps them to
/// canonical IDs through <c>ExternalReference</c> (P3). Expected failures are returned as <see cref="IntegrationErrors"/>;
/// provider outages are thrown.
/// </remarks>
public interface IPosConnector
{
    /// <summary>
    /// Lists the property's outlets (restaurants, bars, in-room dining), in no particular order.
    /// </summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The outlets; empty when there are none.</returns>
    Task<IReadOnlyList<PosOutlet>> ListOutletsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists an outlet's menus with their items, in no particular order.
    /// </summary>
    /// <param name="outletId">The provider's ID for the outlet.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The menus, or <see cref="IntegrationErrors.NotFound"/> for an unknown outlet.</returns>
    Task<Result<IReadOnlyList<PosMenu>>> ListMenusAsync(ExternalId outletId, CancellationToken cancellationToken);
}
