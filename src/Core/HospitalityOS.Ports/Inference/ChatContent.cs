namespace HospitalityOS.Ports.Inference;

/// <summary>
/// A part of a <see cref="ChatMessage"/>: <see cref="ChatText"/>, <see cref="ChatToolCall"/>, or
/// <see cref="ChatToolResult"/>.
/// </summary>
public abstract record ChatContent;
