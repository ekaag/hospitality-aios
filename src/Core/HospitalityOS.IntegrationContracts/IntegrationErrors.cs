using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.IntegrationContracts;

/// <summary>
/// Expected failures every connector reports the same way, so callers handle them without knowing the provider.
/// Outages and other infrastructure faults are thrown, not returned (coding-standards.md §3).
/// </summary>
public static class IntegrationErrors
{
    /// <summary>
    /// Code of <see cref="NotFound"/>.
    /// </summary>
    public const string NotFoundCode = "integration.not_found";

    /// <summary>
    /// Code of <see cref="Rejected"/>.
    /// </summary>
    public const string RejectedCode = "integration.rejected";

    /// <summary>
    /// The provider has no such record for the connector's property.
    /// </summary>
    /// <param name="recordType">What was looked up, e.g. <c>stay</c>.</param>
    /// <param name="id">The provider's ID that was not found.</param>
    /// <returns>A <see cref="ErrorKind.NotFound"/> error.</returns>
    public static Error NotFound(string recordType, ExternalId id) =>
        Error.NotFound(NotFoundCode, $"The provider has no {recordType} '{id}'.");

    /// <summary>
    /// The provider understood the request but refused it, e.g. a message recipient it cannot reach.
    /// </summary>
    /// <param name="reason">The provider's reason, free of personal data.</param>
    /// <returns>An <see cref="ErrorKind.InvalidState"/> error.</returns>
    public static Error Rejected(string reason) => Error.InvalidState(RejectedCode, reason);
}
