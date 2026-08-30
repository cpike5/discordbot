using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;

namespace DiscordBot.Infrastructure.Services.LLM.OpenRouter;

/// <summary>
/// Maps between the provider-agnostic LLM DTOs and OpenRouter's OpenAI-compatible wire records.
/// <para>
/// The three structural differences from the Anthropic shape this replaced: the system prompt is the
/// first message rather than a top-level parameter; tool results are their own <c>"tool"</c>-role
/// messages rather than blocks on a user turn; and tool-call arguments cross the wire as a JSON
/// <em>string</em> rather than a parsed object.
/// </para>
/// </summary>
public static class OpenRouterMessageMapper
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>
    /// Converts an <see cref="LlmRequest"/> into the wire message list, system prompt first.
    /// </summary>
    /// <param name="request">The request whose system prompt and messages to convert.</param>
    /// <param name="enablePromptCaching">
    /// Whether to mark the system message as a cache breakpoint. Honoured by OpenRouter for
    /// Claude-family models and ignored by everything else.
    /// </param>
    /// <returns>Messages in the order OpenRouter expects them.</returns>
    public static List<ChatMessage> ToOpenRouterMessages(LlmRequest request, bool enablePromptCaching)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(CreateSystemMessage(request.SystemPrompt, enablePromptCaching));
        }

        foreach (var msg in request.Messages)
        {
            switch (msg.Role)
            {
                case LlmRole.System:
                    // Unlike the Anthropic shape, a system turn is just another message here.
                    messages.Add(CreateSystemMessage(msg.Content, cached: false));
                    break;

                case LlmRole.User:
                    messages.AddRange(CreateUserMessages(msg));
                    break;

                case LlmRole.Assistant:
                    messages.Add(CreateAssistantMessage(msg));
                    break;

                default:
                    throw new ArgumentException($"Unsupported role: {msg.Role}");
            }
        }

        return messages;
    }

    /// <summary>
    /// Creates the system message, optionally carrying a prompt-cache breakpoint. The cached form
    /// needs the multipart content shape, because <c>cache_control</c> lives on a content part.
    /// </summary>
    public static ChatMessage CreateSystemMessage(string systemPrompt, bool cached) => new()
    {
        Role = "system",
        Content = cached
            ? new List<ContentPart> { ContentPart.TextPart(systemPrompt, CacheControl.Ephemeral) }
            : systemPrompt,
    };

    /// <summary>
    /// Converts one user-role DTO message. Tool results fan out into one <c>"tool"</c>-role message
    /// each — they must directly follow the assistant message whose calls they answer, so they are
    /// emitted before any accompanying user text.
    /// </summary>
    private static IEnumerable<ChatMessage> CreateUserMessages(LlmMessage msg)
    {
        if (msg.ToolResults?.Any() == true)
        {
            foreach (var toolResult in msg.ToolResults)
            {
                yield return new ChatMessage
                {
                    Role = "tool",
                    ToolCallId = toolResult.ToolCallId,
                    // There is no is_error flag on this wire shape; an error is text the model reads.
                    Content = toolResult.IsError
                        ? $"Error: {RawTextOf(toolResult.Content)}"
                        : RawTextOf(toolResult.Content),
                };
            }
        }

        if (!string.IsNullOrEmpty(msg.Content))
        {
            yield return new ChatMessage { Role = "user", Content = msg.Content };
        }
    }

    /// <summary>
    /// Converts one assistant-role DTO message, carrying any tool calls in the OpenAI envelope.
    /// </summary>
    private static ChatMessage CreateAssistantMessage(LlmMessage msg)
    {
        List<ToolCall>? toolCalls = null;

        if (msg.ToolCalls?.Any() == true)
        {
            toolCalls = msg.ToolCalls
                .Select(call => new ToolCall
                {
                    Id = call.Id,
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = call.Name,
                        Arguments = RawTextOf(call.Input),
                    },
                })
                .ToList();
        }

        return new ChatMessage
        {
            Role = "assistant",
            // An assistant message carrying tool calls normally has null content on this wire shape.
            Content = string.IsNullOrEmpty(msg.Content) ? null : msg.Content,
            ToolCalls = toolCalls,
        };
    }

    /// <summary>
    /// Converts tool definitions to the OpenAI function envelope. The JSON Schema crosses over
    /// verbatim — unlike the Anthropic mapping, nothing is decomposed or rebuilt.
    /// </summary>
    /// <param name="tools">The tool definitions to convert.</param>
    /// <returns>Wire tool definitions.</returns>
    public static List<ToolDefinition> ToOpenRouterTools(List<LlmToolDefinition> tools) =>
        tools.Select(t => new ToolDefinition
        {
            Function = new FunctionDefinition
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.InputSchema.ValueKind == JsonValueKind.Undefined
                    ? EmptyObject
                    : t.InputSchema,
            },
        }).ToList();

    /// <summary>
    /// Converts an OpenRouter response to the provider-agnostic <see cref="LlmResponse"/>.
    /// </summary>
    /// <param name="response">The parsed OpenRouter response.</param>
    /// <returns>Provider-agnostic LLM response.</returns>
    public static LlmResponse ToLlmResponse(ChatCompletionResponse response)
    {
        var message = response.Message;

        var toolCalls = message?.ToolCalls?
            .Where(call => !string.IsNullOrWhiteSpace(call.Function?.Name))
            .Select(call => new LlmToolCall
            {
                Id = call.Id ?? string.Empty,
                Name = call.Function!.Name!,
                Input = ParseArguments(call.Function.Arguments),
            })
            .ToList();

        if (toolCalls is { Count: 0 })
        {
            toolCalls = null;
        }

        var usage = response.Usage;

        return new LlmResponse
        {
            Success = true,
            Content = string.IsNullOrEmpty(message?.Content) ? null : message.Content,
            StopReason = MapStopReason(response.FinishReason, toolCalls is { Count: > 0 }),
            ToolCalls = toolCalls,
            Usage = new LlmUsage
            {
                InputTokens = (int)(usage?.PromptTokens ?? 0),
                OutputTokens = (int)(usage?.CompletionTokens ?? 0),
                CachedTokens = (int)(usage?.CacheReadTokens ?? 0),
                CacheWriteTokens = (int)(usage?.CacheWriteTokens ?? 0),
                // OpenRouter reports what it actually billed; null means "not reported", never free.
                EstimatedCost = usage?.Cost,
            },
        };
    }

    /// <summary>
    /// Maps an OpenAI-style <c>finish_reason</c> onto <see cref="LlmStopReason"/>. Tool calls in the
    /// message win over the reported reason: some providers report <c>"stop"</c> on a turn that
    /// nonetheless carries tool calls, and dropping them would strand the agent loop.
    /// </summary>
    private static LlmStopReason MapStopReason(string? finishReason, bool hasToolCalls)
    {
        if (hasToolCalls)
        {
            return LlmStopReason.ToolUse;
        }

        return finishReason switch
        {
            "tool_calls" => LlmStopReason.ToolUse,
            "length" => LlmStopReason.MaxTokens,
            "error" => LlmStopReason.Error,
            // "stop", "content_filter", null and anything unrecognized: the turn is over and
            // whatever text came back is the reply.
            _ => LlmStopReason.EndTurn,
        };
    }

    /// <summary>
    /// Parses a tool call's <c>arguments</c> JSON string into the element the tool registry expects.
    /// A missing or malformed payload degrades to an empty object so the tool runs and reports its
    /// own structured validation failure back to the model, rather than throwing out of the loop.
    /// </summary>
    private static JsonElement ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return EmptyObject;
        }

        try
        {
            using var document = JsonDocument.Parse(arguments);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : EmptyObject;
        }
        catch (JsonException)
        {
            return EmptyObject;
        }
    }

    /// <summary>Raw JSON text of an element, tolerating the uninitialized default.</summary>
    private static string RawTextOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Undefined ? "{}" : element.GetRawText();
}
