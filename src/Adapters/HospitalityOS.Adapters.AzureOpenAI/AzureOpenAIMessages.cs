using System.Text.Json;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;
using OpenAI.Chat;
using PortChatMessage = HospitalityOS.Ports.Inference.ChatMessage;
using PortChatTool = HospitalityOS.Ports.Inference.ChatTool;
using PortChatToolCall = HospitalityOS.Ports.Inference.ChatToolCall;
using SdkChatMessage = OpenAI.Chat.ChatMessage;
using SdkChatTool = OpenAI.Chat.ChatTool;
using SdkChatToolCall = OpenAI.Chat.ChatToolCall;

namespace HospitalityOS.Adapters.AzureOpenAI;

/// <summary>
/// Converts between the platform's chat messages and Chat Completions'. Each tool result becomes its own tool message
/// (wherever the platform's conversation put it), after the assistant message that asked for it. Reasoning is dropped:
/// Chat Completions returns none, and other providers' reasoning means nothing here. A conversation another provider
/// started continues unchanged, since call IDs are opaque.
/// </summary>
internal static class AzureOpenAIMessages
{
    public static List<SdkChatMessage> From(string? systemPrompt, IEnumerable<PortChatMessage> messages)
    {
        List<SdkChatMessage> sent = [];

        if (systemPrompt is not null)
        {
            sent.Add(new SystemChatMessage(systemPrompt));
        }

        foreach (PortChatMessage message in messages)
        {
            string text = message.Text;

            if (message.Role == ChatRole.Assistant)
            {
                List<SdkChatToolCall> calls = [.. message.Content.OfType<PortChatToolCall>().Select(ToToolCall)];

                if (calls.Count > 0)
                {
                    AssistantChatMessage withCalls = new(calls);

                    if (text.Length > 0)
                    {
                        withCalls.Content.Add(ChatMessageContentPart.CreateTextPart(text));
                    }

                    sent.Add(withCalls);
                }
                else if (text.Length > 0)
                {
                    sent.Add(new AssistantChatMessage(text));
                }

                continue;
            }

            sent.AddRange(message.Content.OfType<ChatToolResult>().Select(result =>
                new ToolChatMessage(result.CallId, result.IsError ? "Error: " + result.Content : result.Content)));

            if (text.Length > 0)
            {
                sent.Add(new UserChatMessage(text));
            }
        }

        return sent;
    }

    public static SdkChatTool ToTool(PortChatTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return SdkChatTool.CreateFunctionTool(tool.Name, tool.Description, BinaryData.FromString(tool.InputSchema.GetRawText()));
    }

    public static PortChatMessage ToChatMessage(OpenAI.Chat.ChatCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        List<ChatContent> content = [];
        string text = string.Concat(completion.Content.Where(part => part.Kind == ChatMessageContentPartKind.Text).Select(part => part.Text));

        if (text.Length > 0)
        {
            content.Add(new ChatText(text));
        }

        foreach (SdkChatToolCall call in completion.ToolCalls)
        {
            using JsonDocument arguments = JsonDocument.Parse(call.FunctionArguments.ToMemory());
            content.Add(new PortChatToolCall(call.Id, call.FunctionName, arguments.RootElement.Clone()));
        }

        return new PortChatMessage(ChatRole.Assistant, ValueList.From(content));
    }

    /// <summary>
    /// Whether the schema meets strict structured outputs' rules: every object closed (<c>additionalProperties: false</c>)
    /// and requiring all its properties, all the way down. Strict mode rejects any other schema, so others are sent as a
    /// guide the model follows without the guarantee, and the caller's validation catches a miss.
    /// </summary>
    public static bool IsStrict(JsonElement schema)
    {
        switch (schema.ValueKind)
        {
            case JsonValueKind.Array:
                return schema.EnumerateArray().All(IsStrict);

            case JsonValueKind.Object:
                if (schema.TryGetProperty("properties", out JsonElement properties))
                {
                    HashSet<string> required = schema.TryGetProperty("required", out JsonElement names)
                        ? [.. names.EnumerateArray().Select(name => name.GetString()!)]
                        : [];

                    bool closed = schema.TryGetProperty("additionalProperties", out JsonElement additional) && additional.ValueKind == JsonValueKind.False;

                    if (!closed || properties.EnumerateObject().Any(property => !required.Contains(property.Name)))
                    {
                        return false;
                    }
                }

                return schema.EnumerateObject().All(member => IsStrict(member.Value));

            default:
                return true;
        }
    }

    private static SdkChatToolCall ToToolCall(PortChatToolCall call) =>
        SdkChatToolCall.CreateFunctionToolCall(call.CallId, call.Name, BinaryData.FromString(call.Arguments.GetRawText()));
}
