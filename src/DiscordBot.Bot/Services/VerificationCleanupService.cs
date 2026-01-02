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

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                UpdateHeartbeat();

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

                    ClearError();
                }
                catch (Exception ex)
                {
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
}
