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

        // One at a time: these queries share a single scoped DbContext, which allows one
        // operation at once (running them with Task.WhenAll intermittently threw
        // "A second operation was started on this context instance" and failed the tab)
        var usageOverTime = await GetUsageOverTimeAsync(start, end, guildId, cancellationToken);
        // All four cover the same window and guild: the totals, rates and rankings on the page must
        // describe the same set of commands as the chart
        var successRate = await _commandLogRepository.GetSuccessRateAsync(start, end, guildId, cancellationToken);
        var performance = await _commandLogRepository.GetCommandPerformanceAsync(start, end, guildId, 10, cancellationToken);
        var topCommands = await GetTopCommandsInWindowAsync(start, end, guildId, 10, cancellationToken);

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
        return await GetTopCommandsInWindowAsync(since, null, guildId, limit, cancellationToken);
    }

    private async Task<IDictionary<string, int>> GetTopCommandsInWindowAsync(
        DateTime? since,
        DateTime? until,
        ulong? guildId,
        int limit,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving top {Limit} commands since {Since} until {Until} for guild {GuildId}",
            limit, since, until, guildId);

        var allStats = await _commandLogRepository.GetCommandUsageStatsAsync(since, until, guildId, cancellationToken);

        var topCommands = allStats
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .ToDictionary(x => x.Key, x => x.Value);

        _logger.LogInformation("Retrieved top {Count} commands out of {TotalCount} total commands",
            topCommands.Count, allStats.Count);

        return topCommands;
    }
}
