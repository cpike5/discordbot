using DiscordBot.Core.DTOs;
using DiscordBot.Bot.ViewModels.Components;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying bot status information on the dashboard.
/// </summary>
public record BotStatusViewModel
{
    /// <summary>
    /// Gets the bot's formatted uptime string.
    /// </summary>
    public string UptimeFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of guilds the bot is connected to.
    /// </summary>
    public int GuildCount { get; init; }

    /// <summary>
    /// Gets the bot's current latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; init; }

    /// <summary>
    /// Gets the time when the bot started.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the bot's username.
    /// </summary>
    public string BotUsername { get; init; } = string.Empty;

    /// <summary>
    /// Gets the bot's connection state.
    /// </summary>
    public string ConnectionState { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the bot is currently online.
    /// </summary>
    public bool IsOnline { get; init; }

    /// <summary>
    /// Gets the status type for visual representation.
    /// </summary>
    public StatusType StatusType { get; init; }

    /// <summary>
    /// Creates a <see cref="BotStatusViewModel"/> from a <see cref="BotStatusDto"/>.
    /// </summary>
    /// <param name="dto">The bot status DTO to map from.</param>
    /// <returns>A new <see cref="BotStatusViewModel"/> instance.</returns>
    public static BotStatusViewModel FromDto(BotStatusDto dto)
    {
        return new BotStatusViewModel
        {
            UptimeFormatted = FormatUptime(dto.Uptime),
            GuildCount = dto.GuildCount,
            LatencyMs = dto.LatencyMs,
            StartTime = dto.StartTime,
            BotUsername = dto.BotUsername,
            ConnectionState = dto.ConnectionState,
            IsOnline = dto.ConnectionState.Equals("Connected", StringComparison.OrdinalIgnoreCase),
            StatusType = MapConnectionStateToStatusType(dto.ConnectionState)
        };
    }

    /// <summary>
    /// Formats a TimeSpan into a human-readable uptime string.
    /// </summary>
    /// <param name="uptime">The uptime duration.</param>
    /// <returns>Formatted uptime string (e.g., "2d 5h 30m" or "3h 45m" or "25m").</returns>
    public static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{uptime.Hours}h {uptime.Minutes}m";
        }

        return $"{uptime.Minutes}m";
    }

    /// <summary>
    /// Maps Discord connection state to a status type for visual representation.
    /// </summary>
    /// <param name="connectionState">The connection state string from Discord.</param>
    /// <returns>The corresponding <see cref="StatusType"/>.</returns>
    private static StatusType MapConnectionStateToStatusType(string connectionState)
    {
        return connectionState.ToUpperInvariant() switch
        {
            "CONNECTED" => StatusType.Online,
            "CONNECTING" => StatusType.Idle,
            "DISCONNECTING" => StatusType.Busy,
            _ => StatusType.Offline
        };
    }
}
