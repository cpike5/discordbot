using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;

namespace DiscordBot.Infrastructure.Services.LLM.Implementations;

/// <summary>
/// Static tool definitions for user and guild information access.
/// Defines the schema and metadata for user/guild info tools.
/// </summary>
public static class UserGuildInfoTools
{
    /// <summary>
    /// Tool name for getting user profile information.
    /// </summary>
    public const string GetUserProfile = "get_user_profile";

    /// <summary>
    /// Tool name for getting guild information.
    /// </summary>
    public const string GetGuildInfo = "get_guild_info";

    /// <summary>
    /// Tool name for getting user roles in a guild.
    /// </summary>
    public const string GetUserRoles = "get_user_roles";

    /// <summary>
    /// Gets all user/guild info tool definitions.
    /// </summary>
    /// <returns>Collection of tool definitions.</returns>
    public static IEnumerable<LlmToolDefinition> GetAllTools()
    {
        yield return CreateGetUserProfileTool();
        yield return CreateGetGuildInfoTool();
        yield return CreateGetUserRolesTool();
    }

    /// <summary>
    /// Creates the get_user_profile tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetUserProfileTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID (snowflake) to get profile information for. If not provided, returns info for the requesting user."
                    },
                    "include_roles": {
                        "type": "boolean",
                        "description": "Whether to include the user's roles in the current guild. Default is false.",
                        "default": false
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetUserProfile,
            Description = "Gets basic profile information for a Discord user including username, avatar URL, account creation date, and optionally their roles in the current guild. Use this when users ask about themselves or other users.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    /// <summary>
    /// Creates the get_guild_info tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetGuildInfoTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild_id": {
                        "type": "string",
                        "description": "The Discord guild ID (snowflake) to get information for. If not provided, returns info for the current guild."
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetGuildInfo,
            Description = "Gets information about a Discord guild (server) including name, icon, creation date, member count, and owner. Use this when users ask about the server or its configuration.",
            InputSchema = schema.RootElement.Clone()
        };
    }

    /// <summary>
    /// Creates the get_user_roles tool definition.
    /// </summary>
    private static LlmToolDefinition CreateGetUserRolesTool()
    {
        var schema = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID (snowflake) to get roles for. If not provided, returns roles for the requesting user."
                    }
                },
                "required": []
            }
            """);

        return new LlmToolDefinition
        {
            Name = GetUserRoles,
            Description = "Gets all roles for a user in the current guild, including role names, colors, and hierarchy positions. Use this when users ask about their permissions, roles, or what they can access.",
            InputSchema = schema.RootElement.Clone()
        };
    }
}
