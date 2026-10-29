namespace HospitalityOS.Contracts.Api.Conversations;

/// <summary>
/// A guest's message to the property.
/// </summary>
/// <param name="Text">The message; required, at most 4000 characters.</param>
/// <param name="ClientMessageId">The client's own ID for the message, at most 100 characters: sending the same ID again
/// returns the message already recorded instead of recording it twice. Optional, but clients that retry should send it.</param>
public sealed record PostMessageRequest(string? Text, string? ClientMessageId = null);
