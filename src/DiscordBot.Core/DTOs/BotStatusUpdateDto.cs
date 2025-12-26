namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for real-time bot status updates.
/// </summary>
public class BotStatusUpdateDto
{
    /// <summary>
    /// Gets or sets the bot's current connection state.
    /// Examples: "Connected", "Connecting", "Disconnected"
    /// </summary>
    public string ConnectionState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bot's current latency in milliseconds.
    /// </summary>
    public int Latency { get; set; }

    /// <summary>
    /// Gets or sets the number of guilds the bot is connected to.
    /// </summary>
    public int GuildCount { get; set; }

    /// <summary>
    /// Gets or sets the bot's uptime duration.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this status was captured.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
