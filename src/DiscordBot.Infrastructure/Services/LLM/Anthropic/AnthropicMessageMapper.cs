using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;

namespace DiscordBot.Infrastructure.Services.LLM.Anthropic;

/// <summary>
/// Maps between provider-agnostic LLM DTOs and Anthropic SDK types.
/// Uses the official Anthropic package (12.2.0+).
/// </summary>
public static class AnthropicMessageMapper
{
    /// <summary>
    /// Converts LlmRequest messages to Anthropic MessageParam format.
    /// </summary>
    /// <param name="request">The LLM request containing messages to convert.</param>
    /// <returns>List of Anthropic MessageParam objects.</returns>
    public static List<MessageParam> ToAnthropicMessages(LlmRequest request)
    {
        var messages = new List<MessageParam>();

        foreach (var msg in request.Messages)
        {
            switch (msg.Role)
            {
                case LlmRole.User:
                    messages.Add(CreateUserMessage(msg));
                    break;

                case LlmRole.Assistant:
                    messages.Add(CreateAssistantMessage(msg));
                    break;

                case LlmRole.System:
                    // System messages are handled separately in Anthropic API
                    // They should be passed as System parameter, not in messages array
                    throw new ArgumentException(
                        "System role messages should be set via LlmRequest.SystemPrompt, not in Messages list.");

                default:
                    throw new ArgumentException($"Unsupported role: {msg.Role}");
            }
        }

        return messages;
    }

    /// <summary>
    /// Creates a user message, handling both text content and tool results.
    /// </summary>
    private static MessageParam CreateUserMessage(LlmMessage msg)
    {
        var contentBlocks = new List<ContentBlockParam>();

        // Add text content if present
        if (!string.IsNullOrEmpty(msg.Content))
        {
            contentBlocks.Add(new TextBlockParam { Text = msg.Content });
        }

        // Add tool results if present
        if (msg.ToolResults?.Any() == true)
        {
            foreach (var toolResult in msg.ToolResults)
            {
                contentBlocks.Add(new ToolResultBlockParam
                {
                    ToolUseID = toolResult.ToolCallId,
                    Content = toolResult.Content.GetRawText(),
                    IsError = toolResult.IsError
                });
            }
        }

        return new MessageParam
        {
            Role = Role.User,
            Content = contentBlocks
        };
    }

    /// <summary>
    /// Creates an assistant message, handling both text content and tool calls.
    /// </summary>
    private static MessageParam CreateAssistantMessage(LlmMessage msg)
    {
        var contentBlocks = new List<ContentBlockParam>();

        // Add text content if present
        if (!string.IsNullOrEmpty(msg.Content))
        {
            contentBlocks.Add(new TextBlockParam { Text = msg.Content });
        }

        // Add tool use blocks if present
        if (msg.ToolCalls?.Any() == true)
        {
            foreach (var toolCall in msg.ToolCalls)
            {
                // Deserialize JsonElement to IReadOnlyDictionary<string, JsonElement> for the Input property
                var inputDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    toolCall.Input.GetRawText()) ?? new Dictionary<string, JsonElement>();

                contentBlocks.Add(new ToolUseBlockParam
                {
                    ID = toolCall.Id,
                    Name = toolCall.Name,
                    Input = inputDict
                });
            }
        }

        return new MessageParam
        {
            Role = Role.Assistant,
            Content = contentBlocks
        };
    }

    /// <summary>
    /// Converts LLM tool definitions to Anthropic Tool format.
    /// </summary>
    /// <param name="tools">The tool definitions to convert.</param>
    /// <returns>List of Anthropic Tool objects.</returns>
    public static List<ToolUnion> ToAnthropicTools(List<LlmToolDefinition> tools)
    {
        return tools.Select(t =>
        {
            // Parse the input schema JSON to extract properties and required fields
            var schemaDoc = JsonDocument.Parse(t.InputSchema.GetRawText());
            var schemaRoot = schemaDoc.RootElement;

            var properties = new Dictionary<string, JsonElement>();
            List<string>? required = null;

            if (schemaRoot.TryGetProperty("properties", out var propsElement))
            {
                foreach (var prop in propsElement.EnumerateObject())
                {
                    properties[prop.Name] = prop.Value.Clone();
                }
            }

            if (schemaRoot.TryGetProperty("required", out var reqElement))
            {
                required = reqElement.EnumerateArray()
                    .Select(e => e.GetString()!)
                    .ToList();
            }

            var tool = new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = new InputSchema
                {
                    Properties = properties,
                    Required = required
                }
            };

            return (ToolUnion)tool;
        }).ToList();
    }

    /// <summary>
    /// Converts Anthropic Message response to provider-agnostic LlmResponse.
    /// </summary>
    /// <param name="response">The Anthropic message response.</param>
    /// <returns>Provider-agnostic LLM response.</returns>
    public static LlmResponse ToLlmResponse(Message response)
    {
        // Map stop reason - compare values since StopReason is an ApiEnum
        LlmStopReason stopReason;
        if (response.StopReason == StopReason.EndTurn)
            stopReason = LlmStopReason.EndTurn;
        else if (response.StopReason == StopReason.ToolUse)
            stopReason = LlmStopReason.ToolUse;
        else if (response.StopReason == StopReason.MaxTokens)
            stopReason = LlmStopReason.MaxTokens;
        else if (response.StopReason == StopReason.StopSequence)
            stopReason = LlmStopReason.EndTurn;
        else
            stopReason = LlmStopReason.Error;

        // Extract text content and tool calls from content blocks
        string? textContent = null;
        List<LlmToolCall>? toolCalls = null;

        if (response.Content?.Any() == true)
        {
            var textBlocks = new List<string>();
            var toolUseBlocks = new List<LlmToolCall>();

            foreach (var block in response.Content)
            {
                if (block.Value is TextBlock textBlock)
                {
                    textBlocks.Add(textBlock.Text);
                }
                else if (block.Value is ToolUseBlock toolUseBlock)
                {
                    // Convert the Input dictionary back to JsonElement
                    var inputJson = JsonSerializer.Serialize(toolUseBlock.Input);
                    var inputElement = JsonDocument.Parse(inputJson).RootElement;

                    toolUseBlocks.Add(new LlmToolCall
                    {
                        Id = toolUseBlock.ID,
                        Name = toolUseBlock.Name,
                        Input = inputElement.Clone()
                    });
                }
            }

            if (textBlocks.Any())
            {
                textContent = string.Join("\n", textBlocks);
            }

            if (toolUseBlocks.Any())
            {
                toolCalls = toolUseBlocks;
            }
        }

        // Map usage information (cast long to int for our DTOs)
        var usage = new LlmUsage
        {
            InputTokens = (int)response.Usage.InputTokens,
            OutputTokens = (int)response.Usage.OutputTokens,
            CachedTokens = (int)(response.Usage.CacheReadInputTokens ?? 0),
            CacheWriteTokens = (int)(response.Usage.CacheCreationInputTokens ?? 0)
        };

        return new LlmResponse
        {
            Success = true,
            Content = textContent,
            StopReason = stopReason,
            ToolCalls = toolCalls,
            Usage = usage
        };
    }

    /// <summary>
    /// Creates a system message with cache control for prompt caching.
    /// </summary>
    /// <param name="systemPrompt">The system prompt text.</param>
    /// <returns>List of TextBlockParam with cache control enabled.</returns>
    public static List<TextBlockParam> CreateCachedSystemMessage(string systemPrompt)
    {
        return new List<TextBlockParam>
        {
            new TextBlockParam
            {
                Text = systemPrompt,
                CacheControl = new CacheControlEphemeral()
            }
        };
    }

    /// <summary>
    /// Creates a system message without cache control.
    /// </summary>
    /// <param name="systemPrompt">The system prompt text.</param>
    /// <returns>List of TextBlockParam.</returns>
    public static List<TextBlockParam> CreateSystemMessage(string systemPrompt)
    {
        return new List<TextBlockParam>
        {
            new TextBlockParam { Text = systemPrompt }
        };
    }
}
