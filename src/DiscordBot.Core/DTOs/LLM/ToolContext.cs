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
}
