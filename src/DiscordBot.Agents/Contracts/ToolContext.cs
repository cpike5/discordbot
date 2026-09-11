namespace DiscordBot.Agents.Contracts;

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
    /// Open-ended, application-specific state for this run, keyed by string. The engine never reads
    /// it; it exists so the hosting application can hand its own tools context the engine has no
    /// concept of, without the contract growing an app-specific property per feature.
    /// </summary>
    public Dictionary<string, object?> Items { get; } = new();
}
