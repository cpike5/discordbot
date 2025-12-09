using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up expired interaction states.
/// Runs every 1 minute to remove expired state entries.
/// </summary>
public class InteractionStateCleanupService : BackgroundService
{
    private readonly IInteractionStateService _stateService;
    private readonly ILogger<InteractionStateCleanupService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    public InteractionStateCleanupService(
        IInteractionStateService stateService,
        ILogger<InteractionStateCleanupService> logger)
    {
        _stateService = stateService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Interaction state cleanup service started, cleanup interval: {Interval}",
            CleanupInterval);

        using var timer = new PeriodicTimer(CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var removedCount = _stateService.CleanupExpired();
                    var activeCount = _stateService.ActiveStateCount;

                    _logger.LogTrace(
                        "Cleanup completed: removed {RemovedCount} expired states, {ActiveCount} active states remaining",
                        removedCount,
                        activeCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during interaction state cleanup");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Interaction state cleanup service is stopping");
        }
    }
}
