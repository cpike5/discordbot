using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for detecting raid patterns (coordinated attacks on a server).
/// </summary>
public interface IRaidDetectionService
{
    /// <summary>
    /// Analyzes a user join event for raid patterns.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="accountCreated">The account creation timestamp.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A detection result DTO if raid activity is detected, null otherwise.</returns>
    Task<DetectionResultDto?> AnalyzeJoinAsync(ulong guildId, ulong userId, DateTime accountCreated, CancellationToken ct = default);

    /// <summary>
    /// Records a user join for tracking purposes.
    /// This is used internally to track join rates for raid detection.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="joinTime">The join timestamp.</param>
    void RecordJoin(ulong guildId, ulong userId, DateTime joinTime);

    /// <summary>
    /// Gets the number of recent joins in a time window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="window">The time window.</param>
    /// <returns>The join count.</returns>
    int GetRecentJoinCount(ulong guildId, TimeSpan window);

    /// <summary>
    /// Triggers a server lockdown (restricts new member joins).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task TriggerLockdownAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Lifts a server lockdown (restores normal join permissions).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LiftLockdownAsync(ulong guildId, CancellationToken ct = default);
}
