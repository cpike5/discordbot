using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for DM moderation lookup operations.
/// Defines the schema and metadata for moderation case search and user history tools.
/// </summary>
public static class DmModerationTools
{
    public const string GetModerationCases = "get_moderation_cases";
    public const string GetUserModHistory = "get_user_mod_history";

    /// <summary>
    /// Gets all DM moderation tool definitions.
    /// </summary>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateGetModerationCasesTool();
        yield return CreateGetUserModHistoryTool();
    }

    private static LlmToolDefinition CreateGetModerationCasesTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild_id": {
                        "type": "string",
                        "description": "The guild ID to search cases for. Uses the active guild if not specified."
                    },
                    "type": {
                        "type": "string",
                        "description": "Filter by case type: Warn, Kick, Ban, Mute, Note, Unban."
                    },
                    "target_user_id": {
                        "type": "string",
                        "description": "Filter by the target user's Discord ID."
                    },
                    "moderator_id": {
                        "type": "string",
                        "description": "Filter by the moderator's Discord ID."
                    },
                    "start_date": {
                        "type": "string",
                        "description": "Start date filter in ISO 8601 format (e.g., '2024-01-01')."
                    },
                    "end_date": {
                        "type": "string",
                        "description": "End date filter in ISO 8601 format."
                    },
                    "page": {
                        "type": "integer",
                        "description": "Page number (1-based). Default: 1.",
                        "default": 1,
                        "minimum": 1
                    },
                    "page_size": {
                        "type": "integer",
                        "description": "Results per page. Default: 10, maximum: 25.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 25
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetModerationCases,
            Description = "Searches moderation cases for a guild with optional filters. Supports filtering by case type (Warn, Kick, Ban, Mute, Note, Unban), target user, moderator, and date range. Requires an active guild context or explicit guild_id.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateGetUserModHistoryTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID to look up moderation history for."
                    },
                    "guild_id": {
                        "type": "string",
                        "description": "The guild ID to search in. Uses the active guild if not specified."
                    }
                },
                "required": ["user_id"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetUserModHistory,
            Description = "Gets a comprehensive moderation history for a specific user in a guild. Returns all moderation cases, moderator notes, and watchlist status in a unified view. Requires an active guild context or explicit guild_id.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
