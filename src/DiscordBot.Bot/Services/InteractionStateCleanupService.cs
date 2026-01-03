using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up expired interaction states.
/// Runs every 1 minute to remove expired state entries.
/// </summary>
public class InteractionStateCleanupService : MonitoredBackgroundService
{
    private readonly IInteractionStateService _stateService;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Interaction State Cleanup Service";

    protected virtual string TracingServiceName => "interaction_state_cleanup_service";

    public InteractionStateCleanupService(
        IServiceProvider serviceProvider,
        IInteractionStateService stateService,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<InteractionStateCleanupService> logger)
        : base(serviceProvider, logger)
    {
        _stateService = stateService;
        _bgOptions = bgOptions;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var cleanupInterval = TimeSpan.FromMinutes(_bgOptions.Value.InteractionStateCleanupIntervalMinutes);

        _logger.LogInformation(
            "Interaction state cleanup service started, cleanup interval: {Interval}",
            cleanupInterval);

        using var timer = new PeriodicTimer(cleanupInterval);
        var executionCycle = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
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
                    using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
                        TracingServiceName,
                        "interaction_states");

                    try
                    {
                        var removedCount = _stateService.CleanupExpired();
                        var activeCount = _stateService.ActiveStateCount;

                        BotActivitySource.SetRecordsDeleted(cleanupActivity, removedCount);
                        BotActivitySource.SetSuccess(cleanupActivity);

                        _logger.LogTrace(
                            "Cleanup completed: removed {RemovedCount} expired states, {ActiveCount} active states remaining",
                            removedCount,
                            activeCount);
                    }
                    catch (Exception ex)
                    {
                        BotActivitySource.RecordException(cleanupActivity, ex);
                        throw;
                    }

                    BotActivitySource.SetSuccess(activity);
                    ClearError();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during interaction state cleanup");
                    BotActivitySource.RecordException(activity, ex);
                    RecordError(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Interaction state cleanup service is stopping");
        }
    }
}
