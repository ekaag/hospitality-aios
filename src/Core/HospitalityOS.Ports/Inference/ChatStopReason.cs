namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Why the model stopped generating.
/// </summary>
public enum ChatStopReason
{
    /// <summary>
    /// It finished its answer.
    /// </summary>
    Completed,

    /// <summary>
    /// It is waiting for the results of the tool calls in its message.
    /// </summary>
    ToolCalls,

    /// <summary>
    /// It reached the output limit; the message may be cut off.
    /// </summary>
    MaxTokens,

    /// <summary>
    /// The provider's safety system stopped or withheld the answer.
    /// </summary>
    ContentFiltered,
}
