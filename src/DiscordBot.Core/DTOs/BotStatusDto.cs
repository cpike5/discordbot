namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object containing bot status information.
/// </summary>
public class BotStatusDto
{
    /// <summary>
    /// Gets or sets the bot's uptime duration.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the number of guilds the bot is connected to.
    /// </summary>
    public int GuildCount { get; set; }

    /// <summary>
    /// Gets or sets the bot's current latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the time when the bot started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the bot's username.
    /// </summary>
    public string BotUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bot's connection state.
    /// </summary>
    public string ConnectionState { get; set; } = string.Empty;
}
