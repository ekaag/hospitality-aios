using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// One turn of a conversation.
/// </summary>
/// <param name="Role">Who it is from.</param>
/// <param name="Content">Its parts, in order.</param>
public sealed record ChatMessage(ChatRole Role, ValueList<ChatContent> Content)
{
    /// <summary>
    /// The message's text parts joined, ignoring tool calls and results.
    /// </summary>
    public string Text => string.Concat(Content.OfType<ChatText>().Select(part => part.Text));

    /// <summary>
    /// A user turn holding <paramref name="text"/>.
    /// </summary>
    public static ChatMessage User(string text) => new(ChatRole.User, [new ChatText(text)]);

    /// <summary>
    /// An assistant turn holding <paramref name="text"/>.
    /// </summary>
    public static ChatMessage Assistant(string text) => new(ChatRole.Assistant, [new ChatText(text)]);
}
