namespace DiscordBot.Core.DTOs.LLM;

/// <summary>
/// Context information passed to tools during execution.
/// </summary>
public class ToolContext
{
    /// <summary>
    /// The Discord user ID making the request.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The Discord guild (server) ID for context.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The Discord channel ID for context.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// The Discord message ID that triggered the request.
    /// </summary>
    public ulong MessageId { get; set; }

    /// <summary>
    /// User's roles in the guild (for permission checks).
    /// </summary>
    public List<string> UserRoles { get; set; } = new();

    /// <summary>
    /// The active guild ID for DM assistant context.
    /// Set via the set_active_guild tool and persisted in IMemoryCache.
    /// Tools that require guild context use this as a fallback when no explicit guild_id is provided.
    /// </summary>
    public ulong? ActiveGuildId { get; set; }
}
