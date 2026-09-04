using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Provides guild audio/voice connection status for the dashboard hub.
/// Extracted from <see cref="DiscordBot.Bot.Hubs.DashboardHub"/> to keep the hub thin.
/// </summary>
public interface IDashboardAudioStatusService
{
    /// <summary>
    /// Gets the current audio status for a guild.
    /// </summary>
    AudioStatusDto GetCurrentAudioStatus(ulong guildId, string? connectionId, string? userName);
}
