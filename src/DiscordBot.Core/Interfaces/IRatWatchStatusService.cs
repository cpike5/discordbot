namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing the bot's status during active Rat Watches.
/// Provides coordination between the Rat Watch feature and bot status display.
/// </summary>
public interface IRatWatchStatusService
{
    /// <summary>
    /// Event raised when the Rat Watch status should be updated.
    /// Subscribers should check if there are active watches and update the bot status accordingly.
    /// </summary>
    event EventHandler? StatusUpdateRequested;

    /// <summary>
    /// Requests a status update check. Call this when a Rat Watch state changes
    /// (watch created, voting started, voting ended, cleared early, etc.).
    /// </summary>
    void RequestStatusUpdate();

    /// <summary>
    /// Checks if there are active Rat Watches and updates the bot status accordingly.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the bot status was set to Rat Watch mode, false if normal status was restored.</returns>
    Task<bool> UpdateBotStatusAsync(CancellationToken ct = default);
}
