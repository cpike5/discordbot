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
/// </summary>
public class AssistantInteractionLogRetentionService : MonitoredBackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
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
        ILogger<AssistantInteractionLogRetentionService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
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

        await Task.Delay(InitialDelay, stoppingToken);

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
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Assistant interaction log retention service stopping");
    }

    /// <summary>
    /// Sweeps each of the three tables in turn, skipping any whose configured retention window
    /// is zero or negative.
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

        var sweeps = new (string Name, int RetentionDays, Func<DateTime, CancellationToken, Task<int>> DeleteAsync)[]
        {
            ("guild assistant interaction logs", guildRetentionDays,
                (cutoff, ct) => assistantInteractionLogRepo.DeleteOlderThanAsync(cutoff, ct)),
            ("DM assistant interaction logs", dmRetentionDays,
                (cutoff, ct) => dmAssistantInteractionLogRepo.DeleteOlderThanAsync(cutoff, ct)),
            // Per the plan (Design > 3 > Retention and purge): the usage ledger follows the guild
            // assistant's interaction-log retention window rather than a new option.
            ("LLM usage records", guildRetentionDays,
                (cutoff, ct) => llmUsageRepo.DeleteOlderThanAsync(cutoff, batchSize, ct))
        };

        var totalDeleted = 0;

        foreach (var (name, retentionDays, deleteAsync) in sweeps)
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
                var deleted = await deleteAsync(cutoff, stoppingToken);
                totalDeleted += deleted;

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Deleted {Count} {Table} older than {RetentionDays} days",
                        deleted, name, retentionDays);
                }

                BotActivitySource.SetRecordsDeleted(cleanupActivity, deleted);
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
}
