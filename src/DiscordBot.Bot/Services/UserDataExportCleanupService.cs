using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that deletes expired personal-data export archives (and anything left in the old
/// public <c>wwwroot/exports</c> folder). Runs once at startup, then hourly.
/// </summary>
public class UserDataExportCleanupService : MonitoredBackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;

    public override string ServiceName => "User Data Export Cleanup Service";

    public UserDataExportCleanupService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<UserDataExportCleanupService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        var executionCycle = 0;

        do
        {
            executionCycle++;
            using var activity = BotActivitySource.StartBackgroundServiceActivity(
                "user_data_export_cleanup_service",
                executionCycle,
                Guid.NewGuid().ToString("N")[..16]);

            UpdateHeartbeat();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var exportService = scope.ServiceProvider.GetRequiredService<IUserDataExportService>();
                var cleaned = await exportService.CleanupExpiredExportsAsync(stoppingToken);

                BotActivitySource.SetRecordsDeleted(activity, cleaned);
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
                _logger.LogError(ex, "Error occurred during user data export cleanup");
                RecordError(ex);
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
