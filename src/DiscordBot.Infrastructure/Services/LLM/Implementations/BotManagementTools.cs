using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for bot management operations.
/// Defines the schema and metadata for guild switching, health checks, and audit log tools.
/// </summary>
public static class BotManagementTools
{
    public const string ListGuilds = "list_guilds";
    public const string SetActiveGuild = "set_active_guild";
    public const string GetBotHealth = "get_bot_health";
    public const string SearchAuditLogs = "search_audit_logs";

    /// <summary>
    /// Gets all bot management tool definitions.
    /// </summary>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateListGuildsTool();
        yield return CreateSetActiveGuildTool();
        yield return CreateGetBotHealthTool();
        yield return CreateSearchAuditLogsTool();
    }

    private static LlmToolDefinition CreateListGuildsTool()
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
            Name = ListGuilds,
            Description = "Lists all Discord guilds (servers) the bot is currently a member of. Returns each guild's ID, name, and member count. Use this to help the user identify which guild to set as active.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateSetActiveGuildTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild": {
                        "type": "string",
                        "description": "The guild ID (numeric string) or a partial guild name to fuzzy-match against the bot's guild list."
                    }
                },
                "required": ["guild"]
            }
            """);

        return new LlmToolDefinition
        {
            Name = SetActiveGuild,
            Description = "Sets the active guild context for subsequent DM commands that require a guild. Accepts a guild ID or a partial name for fuzzy matching. The active guild persists for 24 hours. Use list_guilds first if the user hasn't specified which guild.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateGetBotHealthTool()
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
            Name = GetBotHealth,
            Description = "Returns bot health information including uptime, memory usage, WebSocket latency, connection state, and guild count. Useful for diagnosing connectivity or performance issues.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    private static LlmToolDefinition CreateSearchAuditLogsTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild_id": {
                        "type": "string",
                        "description": "The guild ID to search audit logs for. Uses the active guild if not specified."
                    },
                    "category": {
                        "type": "string",
                        "description": "Filter by audit log category: User, Guild, Configuration, Security, Command, Message, System."
                    },
                    "action": {
                        "type": "string",
                        "description": "Filter by action: Created, Updated, Deleted, Login, Logout, PermissionChanged, SettingChanged, CommandExecuted, MessageDeleted, MessageEdited, UserBanned, UserUnbanned, UserKicked, RoleAssigned, RoleRemoved, BotStarted, BotStopped, BotConnected, BotDisconnected."
                    },
                    "actor_id": {
                        "type": "string",
                        "description": "Filter by the actor (user) who performed the action."
                    },
                    "search_term": {
                        "type": "string",
                        "description": "Free-text search across audit log details."
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
            Name = SearchAuditLogs,
            Description = "Searches the bot's audit logs for a guild. Supports filtering by category, action type, actor, date range, and free-text search. Requires an active guild context or explicit guild_id parameter.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
