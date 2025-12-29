// src/DiscordBot.Bot/ViewModels/Components/BotStatusBannerViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for the bot status banner component displayed on the dashboard.
/// Shows connection status, server/member counts, and key metrics like uptime, version, and latency.
/// </summary>
public record BotStatusBannerViewModel
{
    /// <summary>
    /// Whether the bot is currently online and connected to Discord.
    /// </summary>
    public bool IsOnline { get; init; }

    /// <summary>
    /// Status text description (e.g., "Connected", "Disconnected", "Connecting").
    /// </summary>
    public string StatusText { get; init; } = "Disconnected";

    /// <summary>
    /// Number of servers (guilds) the bot is currently connected to.
    /// </summary>
    public int ServerCount { get; init; }

    /// <summary>
    /// Total number of members across all servers.
    /// </summary>
    public int TotalMembers { get; init; }

    /// <summary>
    /// Formatted uptime duration display (e.g., "14d 3h 27m").
    /// </summary>
    public string UptimeDisplay { get; init; } = "0m";

    /// <summary>
    /// Bot version string (e.g., "v0.3.3").
    /// </summary>
    public string Version { get; init; } = "v0.0.0";

    /// <summary>
    /// Gateway latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; init; }
}
