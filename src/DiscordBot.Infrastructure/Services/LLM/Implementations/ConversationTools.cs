using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for DM conversation management.
/// </summary>
public static class ConversationTools
{
    public const string ClearConversation = "clear_conversation";
    public const string SummarizeConversation = "summarize_conversation";

    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateClearConversationTool();
        yield return CreateSummarizeConversationTool();
    }

    private static LlmToolDefinition CreateClearConversationTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = ClearConversation,
            Description = "Clears the entire DM conversation history for the current user. This removes all stored messages and resets the conversation context. Use this when the user explicitly asks to start fresh or clear their history. This action cannot be undone.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateSummarizeConversationTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = SummarizeConversation,
            Description = "Returns metadata about the current conversation including message count and date range. The conversation content is already in your context - use this tool to get statistics, then generate a summary yourself from the messages you can see.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
