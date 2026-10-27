using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationContracts.Messaging;

/// <summary>
/// Sends messages to guests over a channel such as web chat or SMS (spec §10, Epic 11); the guest notification flow
/// (I5) uses it to tell a guest their request is done. Initial implementation: <c>MockMessaging</c> (E3).
/// </summary>
/// <remarks>
/// An instance serves one property: obtain it from the integration runtime's per-property resolver (E2). Business code
/// never references an implementation (spec §10). IDs in results are the provider's own; the runtime maps them to
/// canonical IDs through <c>ExternalReference</c> (P3). Expected failures are returned as <see cref="IntegrationErrors"/>;
/// provider outages are thrown.
/// </remarks>
public interface IMessagingConnector
{
    /// <summary>
    /// Sends a message, at most once per <see cref="OutboundMessage.IdempotencyKey"/>: sending again with a key the
    /// provider has accepted sends nothing and returns the original receipt (P8, Journey 19).
    /// </summary>
    /// <param name="message">What to send, and to whom.</param>
    /// <param name="cancellationToken">Cancels sending.</param>
    /// <returns>The provider's receipt, or <see cref="IntegrationErrors.Rejected"/> when the provider refuses the message,
    /// e.g. for a recipient it cannot reach.</returns>
    Task<Result<MessageReceipt>> SendAsync(OutboundMessage message, CancellationToken cancellationToken);
}
