using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for command analytics and statistics.
/// </summary>
public interface ICommandAnalyticsService
{
    /// <summary>
    /// Gets comprehensive analytics data for the dashboard.
    /// </summary>
    /// <param name="start">Start date for the analytics period.</param>
    /// <param name="end">End date for the analytics period.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comprehensive analytics data.</returns>
    Task<CommandAnalyticsDto> GetAnalyticsAsync(
        DateTime start,
        DateTime end,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command usage aggregated by day.
    /// </summary>
    /// <param name="start">Start date for the period.</param>
    /// <param name="end">End date for the period.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of daily usage data points.</returns>
    Task<IReadOnlyList<UsageOverTimeDto>> GetUsageOverTimeAsync(
        DateTime start,
        DateTime end,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets success/failure rate statistics.
    /// </summary>
    /// <param name="since">Optional start date. If not provided, returns all-time statistics.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success rate statistics.</returns>
    Task<CommandSuccessRateDto> GetSuccessRateAsync(
        DateTime? since = null,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets response time performance metrics by command.
    /// </summary>
    /// <param name="since">Optional start date. If not provided, returns all-time statistics.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="limit">Maximum number of commands to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of command performance metrics.</returns>
    Task<IReadOnlyList<CommandPerformanceDto>> GetCommandPerformanceAsync(
        DateTime? since = null,
        ulong? guildId = null,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top commands by usage count.
    /// </summary>
    /// <param name="since">Optional start date. If not provided, returns all-time statistics.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="limit">Maximum number of commands to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping command names to their usage counts.</returns>
    Task<IDictionary<string, int>> GetTopCommandsAsync(
        DateTime? since = null,
        ulong? guildId = null,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
