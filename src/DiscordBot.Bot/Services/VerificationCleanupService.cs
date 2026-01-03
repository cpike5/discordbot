using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up expired verification codes.
/// Runs every 5 minutes.
/// </summary>
public class VerificationCleanupService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Verification Cleanup Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "verification_cleanup_service";

    public VerificationCleanupService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<VerificationCleanupService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _bgOptions = bgOptions;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var cleanupInterval = TimeSpan.FromMinutes(_bgOptions.Value.VerificationCleanupIntervalMinutes);

        _logger.LogInformation(
            "Verification cleanup service started, cleanup interval: {Interval}",
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
                    var cleanedCount = await PerformCleanupAsync(stoppingToken);

                    BotActivitySource.SetRecordsDeleted(activity, cleanedCount);
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
                    _logger.LogError(ex, "Error occurred during verification code cleanup");
                    RecordError(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Verification cleanup service is stopping");
        }
    }

    /// <summary>
    /// Performs a single cleanup operation by creating a scoped service and invoking the cleanup method.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during cleanup.</param>
    /// <returns>The number of verification codes deleted.</returns>
    private async Task<int> PerformCleanupAsync(CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "expired_verifications");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var verificationService = scope.ServiceProvider
                .GetRequiredService<IVerificationService>();

            var cleanedCount = await verificationService
                .CleanupExpiredCodesAsync(stoppingToken);

            if (cleanedCount > 0)
            {
                _logger.LogDebug(
                    "Verification cleanup completed: {CleanedCount} codes processed",
                    cleanedCount);
            }

            BotActivitySource.SetRecordsDeleted(cleanupActivity, cleanedCount);
            BotActivitySource.SetSuccess(cleanupActivity);

            return cleanedCount;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(cleanupActivity, ex);
            throw;
        }
    }
}
