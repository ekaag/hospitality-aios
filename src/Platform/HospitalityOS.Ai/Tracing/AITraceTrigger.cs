namespace HospitalityOS.Ai.Tracing;

/// <summary>
/// What started an AI turn.
/// </summary>
/// <param name="Type">What kind of thing it was, e.g. <see cref="GuestMessage"/> or an event type; at most 100 characters.</param>
/// <param name="Id">The message or event, if it has an ID.</param>
/// <param name="Text">Its text, for a message.</param>
public sealed record AITraceTrigger(string Type, Guid? Id, string? Text)
{
    /// <summary>
    /// The trigger type of a guest's message.
    /// </summary>
    public const string GuestMessage = nameof(GuestMessage);
}
