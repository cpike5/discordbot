using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for bot status and control operations.
/// </summary>
public interface IBotService
{
    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    /// <returns>The bot status information.</returns>
    BotStatusDto GetStatus();

    /// <summary>
    /// Gets the list of guilds the bot is currently connected to.
    /// </summary>
    /// <returns>A read-only list of guild information.</returns>
    IReadOnlyList<GuildInfoDto> GetConnectedGuilds();

    /// <summary>
    /// Restarts the bot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shuts down the bot gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
