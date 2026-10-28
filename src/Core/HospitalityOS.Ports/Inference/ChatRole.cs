namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Who a <see cref="ChatMessage"/> is from.
/// </summary>
public enum ChatRole
{
    /// <summary>
    /// The guest or staff member talking to the assistant.
    /// </summary>
    User,

    /// <summary>
    /// The model: text and tool calls.
    /// </summary>
    Assistant,

    /// <summary>
    /// The platform answering the assistant's tool calls (<see cref="ChatToolResult"/>).
    /// </summary>
    Tool,
}
