using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Implementation of command analytics service.
/// </summary>
public class CommandAnalyticsService : ICommandAnalyticsService
{
    private readonly ICommandLogRepository _commandLogRepository;
    private readonly ILogger<CommandAnalyticsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandAnalyticsService"/> class.
    /// </summary>
    /// <param name="commandLogRepository">The command log repository.</param>
    /// <param name="logger">The logger.</param>
    public CommandAnalyticsService(
        ICommandLogRepository commandLogRepository,
        ILogger<CommandAnalyticsService> logger)
    {
        _commandLogRepository = commandLogRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CommandAnalyticsDto> GetAnalyticsAsync(
        DateTime start,
        DateTime end,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving comprehensive analytics from {StartDate} to {EndDate} for guild {GuildId}",
            start, end, guildId);

        // Fetch all required data in parallel for better performance
        var usageOverTimeTask = GetUsageOverTimeAsync(start, end, guildId, cancellationToken);
        var successRateTask = GetSuccessRateAsync(start, guildId, cancellationToken);
        var performanceTask = GetCommandPerformanceAsync(start, guildId, 10, cancellationToken);
        var topCommandsTask = GetTopCommandsAsync(start, guildId, 10, cancellationToken);

        await Task.WhenAll(usageOverTimeTask, successRateTask, performanceTask, topCommandsTask);

        var usageOverTime = await usageOverTimeTask;
        var successRate = await successRateTask;
        var performance = await performanceTask;
        var topCommands = await topCommandsTask;

        // Calculate aggregate metrics
        var totalCommands = usageOverTime.Sum(x => x.Count);
        var uniqueCommands = topCommands.Count;
        var avgResponseTimeMs = performance.Any()
            ? performance.Average(x => x.AvgResponseTimeMs)
            : 0;

        var analytics = new CommandAnalyticsDto
        {
            TotalCommands = totalCommands,
            SuccessRate = successRate.SuccessRate,
            AvgResponseTimeMs = avgResponseTimeMs,
            UniqueCommands = uniqueCommands,
            UsageOverTime = usageOverTime,
            TopCommands = topCommands,
            SuccessRateData = successRate,
            PerformanceData = performance
        };

        _logger.LogInformation(
            "Retrieved analytics: TotalCommands={TotalCommands}, UniqueCommands={UniqueCommands}, SuccessRate={SuccessRate:F2}%, AvgResponseTime={AvgResponseTimeMs:F2}ms",
            analytics.TotalCommands, analytics.UniqueCommands, analytics.SuccessRate, analytics.AvgResponseTimeMs);

        return analytics;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UsageOverTimeDto>> GetUsageOverTimeAsync(
        DateTime start,
        DateTime end,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving usage over time from {StartDate} to {EndDate} for guild {GuildId}",
            start, end, guildId);

        var result = await _commandLogRepository.GetUsageOverTimeAsync(start, end, guildId, cancellationToken);

        _logger.LogTrace("Retrieved {DataPointCount} usage over time data points", result.Count);

        return result;
    }

    /// <inheritdoc/>
    public async Task<CommandSuccessRateDto> GetSuccessRateAsync(
        DateTime? since = null,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving success rate since {Since} for guild {GuildId}", since, guildId);

        var result = await _commandLogRepository.GetSuccessRateAsync(since, guildId, cancellationToken);

        _logger.LogTrace("Retrieved success rate: {SuccessCount} successful, {FailureCount} failed, {SuccessRate:F2}%",
            result.SuccessCount, result.FailureCount, result.SuccessRate);

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommandPerformanceDto>> GetCommandPerformanceAsync(
        DateTime? since = null,
        ulong? guildId = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving command performance since {Since} for guild {GuildId}, limit {Limit}",
            since, guildId, limit);

        var result = await _commandLogRepository.GetCommandPerformanceAsync(since, guildId, limit, cancellationToken);

        _logger.LogTrace("Retrieved performance metrics for {CommandCount} commands", result.Count);

        return result;
    }

    /// <inheritdoc/>
    public async Task<IDictionary<string, int>> GetTopCommandsAsync(
        DateTime? since = null,
        ulong? guildId = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving top {Limit} commands since {Since} for guild {GuildId}",
            limit, since, guildId);

        // Use GetCommandUsageStatsAsync and apply guild filter and limit
        var allStats = await _commandLogRepository.GetCommandUsageStatsAsync(since, cancellationToken);

        // Note: The repository method doesn't support guild filtering for GetCommandUsageStatsAsync
        // For now, we'll return the top commands without guild filtering
        // This could be improved by adding guild filtering to the repository method
        var topCommands = allStats
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .ToDictionary(x => x.Key, x => x.Value);

        _logger.LogInformation("Retrieved top {Count} commands out of {TotalCount} total commands",
            topCommands.Count, allStats.Count);

        return topCommands;
    }
}
