namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Text in a message.
/// </summary>
/// <param name="Text">The text.</param>
public sealed record ChatText(string Text) : ChatContent;
