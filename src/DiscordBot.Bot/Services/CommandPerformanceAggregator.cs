using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that aggregates command performance metrics from the command log repository.
/// Caches results to avoid expensive recalculations on every API request.
/// Implements ICommandPerformanceAggregator for query access.
/// </summary>
public class CommandPerformanceAggregator : MonitoredBackgroundService, ICommandPerformanceAggregator
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly DiscordSocketClient _client;
    private readonly PerformanceMetricsOptions _options;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private Dictionary<string, CommandPerformanceAggregateDto> _cachedAggregates = new();
    private DateTime _cacheExpiry = DateTime.MinValue;
    private TimeSpan _cacheTtl;

    public override string ServiceName => "Command Performance Aggregator";

    protected virtual string TracingServiceName => "command_performance_aggregator";

    public CommandPerformanceAggregator(
        IServiceProvider serviceProvider,
        IServiceScopeFactory serviceScopeFactory,
        DiscordSocketClient client,
        ILogger<CommandPerformanceAggregator> logger,
        IOptions<PerformanceMetricsOptions> options)
        : base(serviceProvider, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _client = client;
        _options = options.Value;
        _cacheTtl = TimeSpan.FromMinutes(_options.CommandAggregationCacheTtlMinutes);
    }

    /// <summary>
    /// Background task that periodically refreshes the cache.
    /// </summary>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CommandPerformanceAggregator background service starting");

        // Wait a bit before first execution
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        var executionCycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            executionCycle++;
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            using var activity = BotActivitySource.StartBackgroundServiceActivity(
                TracingServiceName,
                executionCycle,
                correlationId);

            UpdateHeartbeat();

            try
            {
                var commandCount = await RefreshCacheAsync(stoppingToken);
                BotActivitySource.SetRecordsProcessed(activity, commandCount);
                BotActivitySource.SetSuccess(activity);
                ClearError();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing command performance cache");
                BotActivitySource.RecordException(activity, ex);
                RecordError(ex);
            }

            // Wait for the cache TTL before next refresh
            await Task.Delay(_cacheTtl, stoppingToken);
        }

        _logger.LogInformation("CommandPerformanceAggregator background service stopping");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommandPerformanceAggregateDto>> GetAggregatesAsync(int hours = 24)
    {
        _logger.LogDebug("GetAggregatesAsync called with hours={Hours}", hours);

        // For the default 24-hour window, use the cached data
        if (hours == 24)
        {
            await EnsureCacheValidAsync();

            await _cacheLock.WaitAsync();
            try
            {
                _logger.LogDebug("Returning {Count} cached aggregates", _cachedAggregates.Count);
                return _cachedAggregates.Values.ToList();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // For other time windows, query directly from the database
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();

        var since = DateTime.UtcNow.AddHours(-hours);
        var logs = await repository.GetByDateRangeAsync(since, DateTime.UtcNow);

        _logger.LogDebug("GetAggregatesAsync: Found {LogCount} command logs for {Hours} hours", logs.Count, hours);

        var result = logs
            .GroupBy(l => l.CommandName)
            .Select(g => CalculateAggregate(g.Key, g.ToList()))
            .ToList();

        _logger.LogDebug("GetAggregatesAsync: Returning {Count} aggregates", result.Count);

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SlowestCommandDto>> GetSlowestCommandsAsync(int limit = 10, int hours = 24)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();

        var since = DateTime.UtcNow.AddHours(-hours);
        var logs = await repository.GetByDateRangeAsync(since, DateTime.UtcNow);

        var results = new List<SlowestCommandDto>();

        foreach (var l in logs.OrderByDescending(l => l.ResponseTimeMs).Take(limit))
        {
            // Resolve username - try cache first, then fetch from API
            string? username = null;
            try
            {
                var user = _client.GetUser(l.UserId) ?? await _client.GetUserAsync(l.UserId);
                username = user?.Username;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve username for user {UserId}", l.UserId);
            }

            // Resolve guild name from cache
            string? guildName = null;
            if (l.GuildId.HasValue)
            {
                try
                {
                    var guild = _client.GetGuild(l.GuildId.Value);
                    guildName = guild?.Name;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to resolve guild name for guild {GuildId}", l.GuildId.Value);
                }
            }

            results.Add(new SlowestCommandDto
            {
                CommandName = l.CommandName,
                ExecutedAt = l.ExecutedAt,
                DurationMs = l.ResponseTimeMs,
                UserId = l.UserId,
                Username = username,
                GuildId = l.GuildId,
                GuildName = guildName
            });
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommandThroughputDto>> GetThroughputAsync(int hours = 24, string granularity = "hour")
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();

        var since = DateTime.UtcNow.AddHours(-hours);
        var logs = await repository.GetByDateRangeAsync(since, DateTime.UtcNow);

        if (granularity.Equals("day", StringComparison.OrdinalIgnoreCase))
        {
            return logs
                .GroupBy(l => l.ExecutedAt.Date)
                .Select(g => new CommandThroughputDto
                {
                    Timestamp = g.Key,
                    Count = g.Count(),
                    Granularity = "day"
                })
                .OrderBy(t => t.Timestamp)
                .ToList();
        }
        else // hour
        {
            return logs
                .GroupBy(l => new DateTime(l.ExecutedAt.Year, l.ExecutedAt.Month, l.ExecutedAt.Day, l.ExecutedAt.Hour, 0, 0))
                .Select(g => new CommandThroughputDto
                {
                    Timestamp = g.Key,
                    Count = g.Count(),
                    Granularity = "hour"
                })
                .OrderBy(t => t.Timestamp)
                .ToList();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CommandErrorBreakdownDto>> GetErrorBreakdownAsync(int hours = 24, int limit = 50)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();

        var since = DateTime.UtcNow.AddHours(-hours);
        var logs = await repository.GetByDateRangeAsync(since, DateTime.UtcNow);

        var errorGroups = logs
            .Where(l => !l.Success && !string.IsNullOrEmpty(l.ErrorMessage))
            .GroupBy(l => l.CommandName)
            .Select(g => new
            {
                CommandName = g.Key,
                ErrorCount = g.Count(),
                ErrorMessages = g.GroupBy(l => l.ErrorMessage!)
                                 .ToDictionary(eg => eg.Key, eg => eg.Count())
            })
            .OrderByDescending(x => x.ErrorCount)
            .Take(limit);

        return errorGroups.Select(g => new CommandErrorBreakdownDto
        {
            CommandName = g.CommandName,
            ErrorCount = g.ErrorCount,
            ErrorMessages = g.ErrorMessages
        }).ToList();
    }

    /// <inheritdoc/>
    public void InvalidateCache()
    {
        _cacheLock.Wait();
        try
        {
            _cacheExpiry = DateTime.MinValue;
            _logger.LogInformation("Command performance cache invalidated");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Ensures the cache is valid, refreshing if expired.
    /// </summary>
    private async Task EnsureCacheValidAsync()
    {
        if (DateTime.UtcNow >= _cacheExpiry)
        {
            await RefreshCacheAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Refreshes the aggregation cache from the database.
    /// </summary>
    /// <returns>The number of commands cached.</returns>
    private async Task<int> RefreshCacheAsync(CancellationToken cancellationToken)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check expiry after acquiring lock
            if (DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedAggregates.Count;
            }

            _logger.LogDebug("Refreshing command performance cache");

            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();

            var since = DateTime.UtcNow.AddHours(-24);
            var logs = await repository.GetByDateRangeAsync(since, DateTime.UtcNow, cancellationToken);

            var aggregates = logs
                .GroupBy(l => l.CommandName)
                .Select(g => CalculateAggregate(g.Key, g.ToList()))
                .ToDictionary(a => a.CommandName, a => a);

            _cachedAggregates = aggregates;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheTtl);

            _logger.LogInformation(
                "Command performance cache refreshed: {CommandCount} commands, cache valid until {Expiry}",
                aggregates.Count, _cacheExpiry);

            return aggregates.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh command performance cache");
            return 0;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Calculates performance aggregate for a command from its logs.
    /// </summary>
    private static CommandPerformanceAggregateDto CalculateAggregate(string commandName, List<CommandLog> logs)
    {
        var durations = logs.Select(l => (double)l.ResponseTimeMs).OrderBy(d => d).ToList();
        var errorCount = logs.Count(l => !l.Success);

        return new CommandPerformanceAggregateDto
        {
            CommandName = commandName,
            ExecutionCount = logs.Count,
            AvgMs = durations.Average(),
            MinMs = durations.Min(),
            MaxMs = durations.Max(),
            P50Ms = CalculatePercentile(durations, 50),
            P95Ms = CalculatePercentile(durations, 95),
            P99Ms = CalculatePercentile(durations, 99),
            ErrorRate = logs.Count > 0 ? (double)errorCount / logs.Count * 100.0 : 0.0
        };
    }

    /// <summary>
    /// Calculates the percentile value from a sorted list of doubles.
    /// </summary>
    private static double CalculatePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var index = (int)Math.Ceiling((percentile / 100.0) * sortedValues.Count) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));

        return sortedValues[index];
    }
}
