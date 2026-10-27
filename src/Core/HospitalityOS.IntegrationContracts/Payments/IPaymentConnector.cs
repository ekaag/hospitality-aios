using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationContracts.Payments;

/// <summary>
/// Placeholder for a payment provider (spec §10): defined so the boundary exists, with no Phase 1 implementation.
/// Payments and folio settlement are out of scope, so the contract only reads a payment's status; it never moves money.
/// </summary>
/// <remarks>
/// An instance serves one property: obtain it from the integration runtime's per-property resolver (E2). Business code
/// never references an implementation (spec §10). IDs in results are the provider's own; the runtime maps them to
/// canonical IDs through <c>ExternalReference</c> (P3). Expected failures are returned as <see cref="IntegrationErrors"/>;
/// provider outages are thrown.
/// </remarks>
public interface IPaymentConnector
{
    /// <summary>
    /// Gets one payment by the provider's ID.
    /// </summary>
    /// <param name="paymentId">The provider's ID for the payment.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The payment, or <see cref="IntegrationErrors.NotFound"/>.</returns>
    Task<Result<PaymentRecord>> GetPaymentAsync(ExternalId paymentId, CancellationToken cancellationToken);
}
