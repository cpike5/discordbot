using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for DM assistant analytics access.
/// </summary>
public static class DmAnalyticsTools
{
    public const string GetServerActivitySummary = "get_server_activity_summary";
    public const string GetCommandAnalytics = "get_command_analytics";

    /// <summary>
    /// Gets all analytics tool definitions.
    /// </summary>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateGetServerActivitySummaryTool();
        yield return CreateGetCommandAnalyticsTool();
    }

    private static LlmToolDefinition CreateGetServerActivitySummaryTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "days": {
                        "type": "integer",
                        "description": "Number of days to look back for the activity summary. Default is 7.",
                        "default": 7,
                        "minimum": 1,
                        "maximum": 90
                    },
                    "guild_id": {
                        "type": "string",
                        "description": "Discord guild/server ID. If omitted, uses the active guild set via set_active_guild."
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetServerActivitySummary,
            Description = "Gets a summary of server activity including member counts, message volume, active members, and growth metrics for a specified time period. Requires an active guild context.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateGetCommandAnalyticsTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "days": {
                        "type": "integer",
                        "description": "Number of days to look back for analytics. Default is 7.",
                        "default": 7,
                        "minimum": 1,
                        "maximum": 90
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of top commands to return. Default is 10.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    },
                    "guild_id": {
                        "type": "string",
                        "description": "Discord guild/server ID. If omitted, uses the active guild set via set_active_guild."
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetCommandAnalytics,
            Description = "Gets command usage analytics including top commands by usage count and response time performance metrics. Requires an active guild context.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
