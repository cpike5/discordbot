using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for AI assistant operations.
/// Handles Claude API interactions, tool execution, and response generation.
/// </summary>
public interface IAssistantService
{
    /// <summary>
    /// Processes a user question and returns Claude's response.
    /// </summary>
    /// <param name="guildId">Discord guild ID where the question was asked.</param>
    /// <param name="channelId">Discord channel ID where the question was asked.</param>
    /// <param name="userId">Discord user ID who asked the question.</param>
    /// <param name="messageId">Discord message ID of the question.</param>
    /// <param name="question">The user's question text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing Claude's response and metadata.</returns>
    Task<AssistantResponseResult> AskQuestionAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        string question,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the assistant is enabled for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the assistant is enabled for the guild; otherwise, false.</returns>
    Task<bool> IsEnabledForGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the assistant is allowed in a specific channel.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="channelId">Discord channel ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the assistant is allowed in the channel; otherwise, false.</returns>
    Task<bool> IsAllowedInChannelAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has exceeded their rate limit.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating if the user is rate limited and time until reset.</returns>
    Task<RateLimitCheckResult> CheckRateLimitAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage metrics for a guild on a specific date.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date to retrieve metrics for (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Usage metrics for the specified date, or null if no data exists.</returns>
    Task<AssistantUsageMetrics?> GetUsageMetricsAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage metrics for a guild over a date range.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start date of the range (inclusive, UTC).</param>
    /// <param name="endDate">End date of the range (inclusive, UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of usage metrics for each date in the range.</returns>
    Task<IEnumerable<AssistantUsageMetrics>> GetUsageMetricsRangeAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent interaction logs for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="limit">Maximum number of logs to return (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of recent interaction logs ordered by timestamp descending.</returns>
    Task<IEnumerable<AssistantInteractionLog>> GetRecentInteractionsAsync(
        ulong guildId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
