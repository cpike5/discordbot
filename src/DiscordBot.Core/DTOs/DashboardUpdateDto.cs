namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for real-time dashboard updates.
/// Wraps specific update types for SignalR transmission.
/// </summary>
public class DashboardUpdateDto
{
    /// <summary>
    /// Gets or sets the type of update being sent.
    /// Examples: "BotStatus", "CommandExecuted", "GuildActivity"
    /// </summary>
    public string UpdateType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the update occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the payload containing the actual update data.
    /// Can be BotStatusUpdateDto, CommandExecutedUpdateDto, or GuildActivityUpdateDto.
    /// </summary>
    public object? Payload { get; set; }
}
