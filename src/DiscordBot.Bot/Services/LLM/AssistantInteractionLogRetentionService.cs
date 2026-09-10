using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.LLM;

/// <summary>
/// Background service that sweeps assistant data nobody was cleaning up: guild
/// <c>AssistantInteractionLog</c> rows, DM <c>DmAssistantInteractionLog</c> rows, and the
/// <c>LlmUsageRecord</c> ledger. Each table has its own retention window read from existing
/// options (<see cref="Core.Configuration.Assistant.AssistantPrivacyOptions.InteractionLogRetentionDays"/>
/// for guild interaction logs and the ledger, <see cref="DmAssistantOptions.InteractionLogRetentionDays"/>
/// for DM interaction logs) so this service does not introduce yet another retention-days knob.
/// A retention window of zero or less disables that table's sweep without disabling the others.
/// Each table is deleted in batches of <see cref="LlmOptions.RetentionBatchSize"/> rows, with a
/// brief inter-batch delay (see <see cref="SoundPlayLogRetentionService"/>), so a large backlog
/// never deletes in one unbounded transaction. The startup delay and inter-batch delay are both
/// driven by an injected <see cref="TimeProvider"/> so tests can exercise the sweep without
/// waiting on the real clock.
/// </summary>
public class AssistantInteractionLogRetentionService : MonitoredBackgroundService
{
    private static readonly TimeSpan InterBatchDelay = TimeSpan.FromMilliseconds(100);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<LlmOptions> _llmOptions;
    private readonly IOptions<AssistantOptions> _assistantOptions;
    private readonly IOptions<DmAssistantOptions> _dmAssistantOptions;

    public override string ServiceName => "Assistant Interaction Log Retention Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "assistant_interaction_log_retention_service";

    public AssistantInteractionLogRetentionService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<LlmOptions> llmOptions,
        IOptions<AssistantOptions> assistantOptions,
        IOptions<DmAssistantOptions> dmAssistantOptions,
        ILogger<AssistantInteractionLogRetentionService> logger,
        TimeProvider? timeProvider = null)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _llmOptions = llmOptions;
        _assistantOptions = assistantOptions;
        _dmAssistantOptions = dmAssistantOptions;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        if (_llmOptions.Value.RetentionSweepIntervalHours <= 0)
        {
            _logger.LogInformation("Assistant interaction log retention sweep is disabled via configuration");
            SetStatus("Disabled");
            return;
        }

        _logger.LogInformation(
            "Assistant interaction log retention service starting. Interval: {IntervalHours}h, Batch size: {BatchSize}",
            _llmOptions.Value.RetentionSweepIntervalHours,
            _llmOptions.Value.RetentionBatchSize);

        var initialDelay = TimeSpan.FromMinutes(_llmOptions.Value.RetentionSweepInitialDelayMinutes);
        await Task.Delay(initialDelay, _timeProvider, stoppingToken);

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
                _logger.LogError(ex, "Error during assistant interaction log retention cleanup");
                RecordError(ex);
            }

            var interval = TimeSpan.FromHours(_llmOptions.Value.RetentionSweepIntervalHours);
            await Task.Delay(interval, _timeProvider, stoppingToken);
        }

        _logger.LogInformation("Assistant interaction log retention service stopping");
    }

    /// <summary>
    /// Sweeps each of the three tables in turn, skipping any whose configured retention window
    /// is zero or negative. Each table deletes in batches of <see cref="LlmOptions.RetentionBatchSize"/>
    /// rows until nothing more is deleted, with a brief inter-batch delay so a large backlog
    /// doesn't hold a long-running transaction.
    /// </summary>
    private async Task<int> PerformCleanupAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var assistantInteractionLogRepo = scope.ServiceProvider.GetRequiredService<IAssistantInteractionLogRepository>();
        var dmAssistantInteractionLogRepo = scope.ServiceProvider.GetRequiredService<IDmAssistantInteractionLogRepository>();
        var llmUsageRepo = scope.ServiceProvider.GetRequiredService<ILlmUsageRepository>();

        var guildRetentionDays = _assistantOptions.Value.Privacy.InteractionLogRetentionDays;
        var dmRetentionDays = _dmAssistantOptions.Value.InteractionLogRetentionDays;
        var batchSize = _llmOptions.Value.RetentionBatchSize;

        var sweeps = new (string Name, int RetentionDays, Func<DateTime, int, CancellationToken, Task<int>> DeleteBatchAsync)[]
        {
            ("guild assistant interaction logs", guildRetentionDays,
                (cutoff, batch, ct) => assistantInteractionLogRepo.DeleteOlderThanAsync(cutoff, batch, ct)),
            ("DM assistant interaction logs", dmRetentionDays,
                (cutoff, batch, ct) => dmAssistantInteractionLogRepo.DeleteOlderThanAsync(cutoff, batch, ct)),
            // Per the plan (Design > 3 > Retention and purge): the usage ledger follows the guild
            // assistant's interaction-log retention window rather than a new option.
            ("LLM usage records", guildRetentionDays,
                (cutoff, batch, ct) => llmUsageRepo.DeleteOlderThanAsync(cutoff, batch, ct))
        };

        var totalDeleted = 0;

        foreach (var (name, retentionDays, deleteBatchAsync) in sweeps)
        {
            if (retentionDays <= 0)
            {
                _logger.LogDebug("Skipping {Table} sweep: retention is disabled (days <= 0)", name);
                continue;
            }

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
                TracingServiceName,
                name.Replace(" ", "_"));

            try
            {
                var tableDeleted = await DeleteInBatchesAsync(deleteBatchAsync, cutoff, batchSize, stoppingToken);
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
            catch (Exception ex)
            {
                BotActivitySource.RecordException(cleanupActivity, ex);
                throw;
            }
        }

        return totalDeleted;
    }

    /// <summary>
    /// Repeatedly invokes <paramref name="deleteBatchAsync"/> until a batch deletes fewer rows
    /// than <paramref name="batchSize"/> (i.e. nothing remains), pausing <see cref="InterBatchDelay"/>
    /// between batches. Mirrors <c>SoundPlayLogRetentionService.CleanupPlayLogsAsync</c>.
    /// </summary>
    private async Task<int> DeleteInBatchesAsync(
        Func<DateTime, int, CancellationToken, Task<int>> deleteBatchAsync,
        DateTime cutoff,
        int batchSize,
        CancellationToken stoppingToken)
    {
        var totalDeleted = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var deleted = await deleteBatchAsync(cutoff, batchSize, stoppingToken);

            if (deleted == 0)
            {
                break;
            }

            totalDeleted += deleted;

            if (deleted < batchSize)
            {
                break;
            }

            await Task.Delay(InterBatchDelay, _timeProvider, stoppingToken);
        }

        return totalDeleted;
    }
}
