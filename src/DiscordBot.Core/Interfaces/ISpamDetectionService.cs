using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for detecting spam patterns in messages.
/// </summary>
public interface ISpamDetectionService
{
    /// <summary>
    /// Analyzes a message for spam patterns.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="content">The message content.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="accountCreated">The account creation timestamp.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A detection result DTO if spam is detected, null otherwise.</returns>
    Task<DetectionResultDto?> AnalyzeMessageAsync(ulong guildId, ulong userId, ulong channelId, string content, ulong messageId, DateTime accountCreated, CancellationToken ct = default);

    /// <summary>
    /// Records a message for rate tracking purposes.
    /// This is used internally to track message rates for spam detection.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="contentHash">The message content hash.</param>
    /// <param name="timestamp">The message timestamp.</param>
    void RecordMessage(ulong guildId, ulong userId, string contentHash, DateTime timestamp);

    /// <summary>
    /// Gets the number of messages sent by a user in a time window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="window">The time window.</param>
    /// <returns>The message count.</returns>
    int GetMessageCount(ulong guildId, ulong userId, TimeSpan window);

    /// <summary>
    /// Gets the number of duplicate messages sent by a user in a time window.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="contentHash">The message content hash.</param>
    /// <param name="window">The time window.</param>
    /// <returns>The duplicate count.</returns>
    int GetDuplicateCount(ulong guildId, ulong userId, string contentHash, TimeSpan window);
}
