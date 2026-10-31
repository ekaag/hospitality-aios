namespace HospitalityOS.Contracts.Capabilities.Messaging;

/// <summary>
/// Output of <c>Messaging.NotifyGuest</c>: the message, new or the one already requested with the same key.
/// </summary>
/// <param name="Source">The notification record.</param>
/// <param name="Status">Sent, or Failed with why.</param>
/// <param name="ProviderName">The messaging provider that accepted it.</param>
/// <param name="FailureReason">Why it could not be sent.</param>
public sealed record NotifyGuestOutput(SourceReference Source, NotificationStatus Status, string? ProviderName, string? FailureReason);
