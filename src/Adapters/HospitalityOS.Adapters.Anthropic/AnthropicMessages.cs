using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using HospitalityOS.Ports.Inference;
using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.Adapters.Anthropic;

/// <summary>
/// Converts between the platform's chat messages and the Messages API's. Tool results travel in user turns, so
/// consecutive user and tool messages are merged into one turn (the API alternates user and assistant turns).
/// Reasoning from other providers is dropped; Claude's own thinking is returned exactly as received.
/// </summary>
internal static class AnthropicMessages
{
    public static BetaMessageParam User(string text) => Turn(assistant: false, [new BetaTextBlockParam { Text = text }]);

    public static List<BetaMessageParam> From(IEnumerable<ChatMessage> messages)
    {
        List<BetaMessageParam> turns = [];
        bool? assistant = null;
        List<BetaContentBlockParam> blocks = [];

        foreach (ChatMessage message in messages)
        {
            bool next = message.Role == ChatRole.Assistant;

            if (assistant is { } current && current != next)
            {
                turns.Add(Turn(current, blocks));
                blocks = [];
            }

            assistant = next;
            blocks.AddRange(message.Content.Select(ToBlock).OfType<BetaContentBlockParam>());
        }

        if (assistant is { } last)
        {
            turns.Add(Turn(last, blocks));
        }

        return turns;
    }

    public static BetaToolUnion ToTool(ChatTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return new BetaTool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = new InputSchema
            {
                Properties = tool.InputSchema.TryGetProperty("properties", out JsonElement properties) ? ToDictionary(properties) : new Dictionary<string, JsonElement>(),
                Required = tool.InputSchema.TryGetProperty("required", out JsonElement required) ? [.. required.EnumerateArray().Select(name => name.GetString()!)] : null,
            },
        };
    }

    public static ChatMessage ToChatMessage(BetaMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        List<ChatContent> content = [];

        foreach (BetaContentBlock block in response.Content)
        {
            if (block.TryPickText(out BetaTextBlock? text))
            {
                content.Add(new ChatText(text.Text));
            }
            else if (block.TryPickToolUse(out BetaToolUseBlock? toolUse))
            {
                content.Add(new ChatToolCall(toolUse.ID, toolUse.Name, JsonSerializer.SerializeToElement(toolUse.Input)));
            }
            else if (block.TryPickThinking(out BetaThinkingBlock? thinking))
            {
                content.Add(Reasoning(new ThinkingState("thinking", thinking.Thinking, thinking.Signature, null)));
            }
            else if (block.TryPickRedactedThinking(out BetaRedactedThinkingBlock? redacted))
            {
                content.Add(Reasoning(new ThinkingState("redacted_thinking", null, null, redacted.Data)));
            }

            // Server-side fallback boundaries and other block kinds carry nothing the platform uses.
        }

        return new ChatMessage(ChatRole.Assistant, ValueList.From(content));
    }

    public static Dictionary<string, JsonElement> ToDictionary(JsonElement value) =>
        value.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);

    private static BetaMessageParam Turn(bool assistant, List<BetaContentBlockParam> blocks) =>
        assistant ? new BetaAssistantMessageParam { Content = blocks } : new BetaUserMessageParam { Content = blocks };

    private static BetaContentBlockParam? ToBlock(ChatContent content) =>
        content switch
        {
            ChatText text => new BetaTextBlockParam { Text = text.Text },
            ChatToolCall call => new BetaToolUseBlockParam { ID = call.CallId, Name = call.Name, Input = ToDictionary(call.Arguments) },
            ChatToolResult result => new BetaToolResultBlockParam { ToolUseID = result.CallId, Content = result.Content, IsError = result.IsError },
            ChatReasoning { Provider: AnthropicModel.ProviderName } reasoning => FromReasoning(reasoning),
            _ => null,
        };

    private static ChatReasoning Reasoning(ThinkingState state) =>
        new(AnthropicModel.ProviderName, JsonSerializer.Serialize(state, ThinkingState.Json));

    private static BetaContentBlockParam FromReasoning(ChatReasoning reasoning)
    {
        ThinkingState state = JsonSerializer.Deserialize<ThinkingState>(reasoning.Data, ThinkingState.Json)
            ?? throw new InvalidOperationException("Unreadable Anthropic reasoning state.");

        return state.Type == "redacted_thinking"
            ? new BetaRedactedThinkingBlockParam { Data = state.Data! }
            : new BetaThinkingBlockParam { Thinking = state.Thinking!, Signature = state.Signature! };
    }

    /// <summary>
    /// A thinking block as the API returned it; its signature must come back unchanged.
    /// </summary>
    private sealed record ThinkingState(string Type, string? Thinking, string? Signature, string? Data)
    {
        public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
    }
}
