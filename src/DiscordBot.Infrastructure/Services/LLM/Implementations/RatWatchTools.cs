using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for RatWatch assistant functionality.
/// Defines the schema and metadata for RatWatch-related tools.
/// </summary>
public static class RatWatchTools
{
    /// <summary>
    /// Tool name for getting the RatWatch leaderboard.
    /// </summary>
    public const string GetRatWatchLeaderboard = "get_rat_watch_leaderboard";

    /// <summary>
    /// Tool name for getting RatWatch user statistics.
    /// </summary>
    public const string GetRatWatchUserStats = "get_rat_watch_user_stats";

    /// <summary>
    /// Tool name for getting RatWatch summary.
    /// </summary>
    public const string GetRatWatchSummary = "get_rat_watch_summary";

    /// <summary>
    /// Gets all RatWatch tool definitions.
    /// </summary>
    /// <returns>Collection of tool definitions.</returns>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateGetRatWatchLeaderboardTool();
        yield return CreateGetRatWatchUserStatsTool();
        yield return CreateGetRatWatchSummaryTool();
    }

    /// <summary>
    /// Creates the get_rat_watch_leaderboard tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetRatWatchLeaderboardTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of results to return. Default is 10, maximum is 50.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetRatWatchLeaderboard,
            Description = "Gets the RatWatch leaderboard showing users ranked by their rat incident count in the current guild. Use this when users ask about top rats, leaderboard, rankings, or who has the most rat incidents.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    /// <summary>
    /// Creates the get_rat_watch_user_stats tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetRatWatchUserStatsTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID (snowflake) to get RatWatch statistics for. If not provided, returns stats for the requesting user."
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetRatWatchUserStats,
            Description = "Gets RatWatch statistics for a specific user including their rat incident count, rank, recent incidents, and history in the current guild. Use this when users ask about their own rat stats or another user's rat record.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    /// <summary>
    /// Creates the get_rat_watch_summary tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetRatWatchSummaryTool()
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
            Name = GetRatWatchSummary,
            Description = "Gets an overview summary of RatWatch activity in the current guild including total incidents, active watchers, recent activity, and trends. Use this when users ask about overall rat watch status or guild-wide statistics.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
