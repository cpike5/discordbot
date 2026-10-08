using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that sweeps six high-volume tables that had no retention job: command logs,
/// user activity events, connection events, TTS messages, assistant usage metrics, and audio
/// playback logs. Each table has its own retention window:
/// <list type="bullet">
/// <item>command logs, TTS messages, assistant usage metrics, audio playback logs:
/// <see cref="DataRetentionOptions"/> (<c>DataRetention</c> section);</item>
/// <item>user activity events: <see cref="UserActivityEventRetentionOptions"/>
/// (<c>UserActivityEventRetention</c> section; its <c>Enabled</c>, <c>RetentionDays</c> and
/// <c>CleanupBatchSize</c> apply, the sweep interval is this service's);</item>
/// <item>connection events: <see cref="PerformanceMetricsOptions.ConnectionEventRetentionDays"/>.</item>
/// </list>
/// A retention window of zero or less disables that table's sweep without disabling the others.
/// Each table deletes in batches with a brief inter-batch delay (see
/// <see cref="SoundPlayLogRetentionService"/>), so a large backlog never deletes in one unbounded
/// transaction. The startup and inter-batch delays use an injected <see cref="TimeProvider"/> so
/// tests can drive the sweep without waiting on the real clock (as
/// <see cref="LLM.AssistantInteractionLogRetentionService"/> does).
/// </summary>
public class DataRetentionService : MonitoredBackgroundService
{
    private static readonly TimeSpan InterBatchDelay = TimeSpan.FromMilliseconds(100);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<DataRetentionOptions> _options;
    private readonly IOptions<UserActivityEventRetentionOptions> _activityOptions;
    private readonly IOptions<PerformanceMetricsOptions> _performanceOptions;

    public override string ServiceName => "Data Retention Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "data_retention_service";

    public DataRetentionService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<DataRetentionOptions> options,
        IOptions<UserActivityEventRetentionOptions> activityOptions,
        IOptions<PerformanceMetricsOptions> performanceOptions,
        ILogger<DataRetentionService> logger,
        TimeProvider? timeProvider = null)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _activityOptions = activityOptions;
        _performanceOptions = performanceOptions;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.Enabled || options.CleanupIntervalHours <= 0)
        {
            _logger.LogInformation("Data retention sweep is disabled via configuration");
            SetStatus("Disabled");
            return;
        }

        _logger.LogInformation(
            "Data retention service starting. Interval: {IntervalHours}h, Batch size: {BatchSize}",
            options.CleanupIntervalHours,
            options.CleanupBatchSize);

        await Task.Delay(TimeSpan.FromMinutes(options.InitialDelayMinutes), _timeProvider, stoppingToken);

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
                var totalDeleted = await PerformCleanupAsync(stoppingToken);

                BotActivitySource.SetRecordsDeleted(activity, totalDeleted);
                BotActivitySource.SetSuccess(activity);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                BotActivitySource.RecordException(activity, ex);
                _logger.LogError(ex, "Error during data retention cleanup");
                RecordError(ex);
            }

            await Task.Delay(TimeSpan.FromHours(options.CleanupIntervalHours), _timeProvider, stoppingToken);
        }

        _logger.LogInformation("Data retention service stopping");
    }

    /// <summary>
    /// Sweeps each table in turn, skipping any whose retention window is disabled. A failure on
    /// one table is logged and the remaining tables are still swept; the first failure is
    /// rethrown at the end so the cycle is recorded as an error.
    /// </summary>
    private async Task<int> PerformCleanupAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        var options = _options.Value;
        var activityOptions = _activityOptions.Value;
        var batchSize = options.CleanupBatchSize;

        var sweeps = new (string Name, int RetentionDays, int BatchSize, Func<DateTime, int, CancellationToken, Task<int>> DeleteBatchAsync)[]
        {
            ("command logs", options.CommandLogRetentionDays, batchSize,
                (cutoff, batch, ct) => services.GetRequiredService<ICommandLogRepository>().DeleteOlderThanAsync(cutoff, batch, ct)),
            ("user activity events", activityOptions.Enabled ? activityOptions.RetentionDays : 0, activityOptions.CleanupBatchSize,
                (cutoff, batch, ct) => services.GetRequiredService<IUserActivityEventRepository>().DeleteOlderThanAsync(cutoff, batch, ct)),
            ("connection events", _performanceOptions.Value.ConnectionEventRetentionDays, batchSize,
                (cutoff, batch, ct) => services.GetRequiredService<IConnectionEventRepository>().DeleteOlderThanAsync(cutoff, batch, ct)),
            ("TTS messages", options.TtsMessageRetentionDays, batchSize,
                (cutoff, batch, ct) => services.GetRequiredService<ITtsMessageRepository>().DeleteOlderThanAsync(cutoff, batch, ct)),
            ("assistant usage metrics", options.AssistantUsageMetricsRetentionDays, batchSize,
                (cutoff, batch, ct) => services.GetRequiredService<IAssistantUsageMetricsRepository>().DeleteOlderThanAsync(cutoff, batch, ct)),
            ("audio playback logs", options.AudioPlaybackLogRetentionDays, batchSize,
                (cutoff, batch, ct) => services.GetRequiredService<IAudioPlaybackLogRepository>().DeleteOlderThanAsync(cutoff, batch, ct))
        };

        var totalDeleted = 0;
        Exception? firstFailure = null;

        foreach (var (name, retentionDays, tableBatchSize, deleteBatchAsync) in sweeps)
        {
            if (retentionDays <= 0)
            {
                _logger.LogDebug("Skipping {Table} sweep: retention is disabled", name);
                continue;
            }

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
                TracingServiceName,
                name.Replace(" ", "_"));

            try
            {
                var tableDeleted = await DeleteInBatchesAsync(deleteBatchAsync, cutoff, tableBatchSize, stoppingToken);
                totalDeleted += tableDeleted;

                if (tableDeleted > 0)
                {
                    _logger.LogInformation(
                        "Deleted {Count} {Table} older than {RetentionDays} days",
                        tableDeleted, name, retentionDays);
                }

                BotActivitySource.SetRecordsDeleted(cleanupActivity, tableDeleted);
                BotActivitySource.SetSuccess(cleanupActivity);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                BotActivitySource.RecordException(cleanupActivity, ex);
                _logger.LogError(ex, "Error sweeping {Table}; continuing with the remaining tables", name);
                firstFailure ??= ex;
            }
        }

        if (firstFailure is not null)
        {
            throw firstFailure;
        }

        return totalDeleted;
    }

    /// <summary>
    /// Repeatedly invokes <paramref name="deleteBatchAsync"/> until a batch deletes fewer rows
    /// than <paramref name="batchSize"/>, pausing <see cref="InterBatchDelay"/> between batches.
    /// The repositories clamp the batch size to 1000, so the stop condition uses the same clamp.
    /// </summary>
    private async Task<int> DeleteInBatchesAsync(
        Func<DateTime, int, CancellationToken, Task<int>> deleteBatchAsync,
        DateTime cutoff,
        int batchSize,
        CancellationToken stoppingToken)
    {
        var effectiveBatchSize = Math.Clamp(batchSize, 1, 1000);
        var totalDeleted = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var deleted = await deleteBatchAsync(cutoff, effectiveBatchSize, stoppingToken);
            totalDeleted += deleted;

            if (deleted < effectiveBatchSize)
            {
                break;
            }

            await Task.Delay(InterBatchDelay, _timeProvider, stoppingToken);
        }

        return totalDeleted;
    }
}
