using DiscordBot.Bot.Metrics;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically updates business metrics and SLO metrics.
/// Runs less frequently than real-time metrics (every 5 minutes) to reduce database load.
/// </summary>
public class BusinessMetricsUpdateService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly BusinessMetrics _businessMetrics;
    private readonly SloMetrics _sloMetrics;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Business Metrics Update Service";

    public BusinessMetricsUpdateService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory serviceScopeFactory,
        BusinessMetrics businessMetrics,
        SloMetrics sloMetrics,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<BusinessMetricsUpdateService> logger)
        : base(serviceProvider, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _businessMetrics = businessMetrics;
        _sloMetrics = sloMetrics;
        _bgOptions = bgOptions;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var updateInterval = TimeSpan.FromMinutes(_bgOptions.Value.BusinessMetricsUpdateIntervalMinutes);
        var initialDelay = TimeSpan.FromSeconds(_bgOptions.Value.BusinessMetricsInitialDelaySeconds);

        _logger.LogInformation("Business metrics update service starting, will update every {Interval} minutes", updateInterval.TotalMinutes);

        // Wait a bit before first execution to allow the application to fully start
        await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            UpdateHeartbeat();

            try
            {
                await UpdateMetricsAsync(stoppingToken);
                ClearError();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating business and SLO metrics");
                RecordError(ex);
            }

            await Task.Delay(updateInterval, stoppingToken);
        }

        _logger.LogInformation("Business metrics update service stopping");
    }

    private async Task UpdateMetricsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var commandLogRepository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();
        var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();

        var now = DateTime.UtcNow;
        var startOfToday = now.Date;
        var sevenDaysAgo = now.AddDays(-7);
        var twentyFourHoursAgo = now.AddHours(-24);
        var oneHourAgo = now.AddHours(-1);
        var thirtyDaysAgo = now.AddDays(-30);

        try
        {
            // Update business metrics
            await UpdateBusinessMetricsAsync(
                commandLogRepository,
                guildRepository,
                startOfToday,
                sevenDaysAgo,
                cancellationToken);

            // Update SLO metrics
            await UpdateSloMetricsAsync(
                commandLogRepository,
                twentyFourHoursAgo,
                oneHourAgo,
                thirtyDaysAgo,
                cancellationToken);

            _logger.LogTrace("Successfully updated business and SLO metrics");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update metrics");
            throw;
        }
    }

    private async Task UpdateBusinessMetricsAsync(
        ICommandLogRepository commandLogRepository,
        IGuildRepository guildRepository,
        DateTime startOfToday,
        DateTime sevenDaysAgo,
        CancellationToken cancellationToken)
    {
        // Guilds joined today
        var guildsJoinedToday = await guildRepository.GetJoinedCountAsync(startOfToday, cancellationToken);
        _businessMetrics.UpdateGuildsJoinedToday(guildsJoinedToday);

        // Guilds left today (requires schema changes - currently returns 0)
        var guildsLeftToday = await guildRepository.GetLeftCountAsync(startOfToday, cancellationToken);
        _businessMetrics.UpdateGuildsLeftToday(guildsLeftToday);

        // Active guilds daily (guilds with command activity today)
        var activeGuildsDaily = await commandLogRepository.GetActiveGuildCountAsync(startOfToday, cancellationToken);
        _businessMetrics.UpdateActiveGuildsDaily(activeGuildsDaily);

        // Active users in last 7 days
        var activeUsers7d = await commandLogRepository.GetUniqueUserCountAsync(sevenDaysAgo, cancellationToken);
        _businessMetrics.UpdateActiveUsers7d(activeUsers7d);

        // Commands executed today
        var commandsToday = await commandLogRepository.GetCommandCountAsync(startOfToday, cancellationToken);
        _businessMetrics.UpdateCommandsToday(commandsToday);

        _logger.LogDebug(
            "Business metrics updated: GuildsJoinedToday={GuildsJoined}, GuildsLeftToday={GuildsLeft}, ActiveGuildsDaily={ActiveGuilds}, ActiveUsers7d={ActiveUsers}, CommandsToday={Commands}",
            guildsJoinedToday, guildsLeftToday, activeGuildsDaily, activeUsers7d, commandsToday);
    }

    private async Task UpdateSloMetricsAsync(
        ICommandLogRepository commandLogRepository,
        DateTime twentyFourHoursAgo,
        DateTime oneHourAgo,
        DateTime thirtyDaysAgo,
        CancellationToken cancellationToken)
    {
        // Command success rate (last 24 hours)
        var successRate24h = await commandLogRepository.GetSuccessRateAsync(
            since: twentyFourHoursAgo,
            cancellationToken: cancellationToken);
        _sloMetrics.UpdateCommandSuccessRate24h((double)successRate24h.SuccessRate);

        // API success rate (last 24 hours)
        // Note: This would require tracking API request logs similar to command logs
        // For now, we'll use a placeholder or calculate from command success rate
        // TODO: Implement API request logging for accurate API success rate
        _sloMetrics.UpdateApiSuccessRate24h((double)successRate24h.SuccessRate);

        // P99 latency (last 1 hour)
        // Calculate p99 from command performance data
        var performanceMetrics = await commandLogRepository.GetCommandPerformanceAsync(
            since: oneHourAgo,
            limit: int.MaxValue,
            cancellationToken: cancellationToken);

        var p99Latency = performanceMetrics.Any()
            ? performanceMetrics.Max(m => m.MaxResponseTimeMs)
            : 0;
        _sloMetrics.UpdateCommandP99Latency1h(p99Latency);

        // Error budget remaining (assumes 99.9% SLO)
        // Error budget = (1 - target SLO) * total requests
        // Remaining budget % = (budget - errors used) / budget * 100
        var targetSlo = 99.9;
        var successRateValue = (double)successRate24h.SuccessRate;
        var errorBudgetRemaining = successRateValue >= targetSlo
            ? 100.0
            : Math.Max(0, (successRateValue - (targetSlo - 0.1)) / 0.1 * 100);
        _sloMetrics.UpdateErrorBudgetRemaining(errorBudgetRemaining);

        // Uptime percentage (last 30 days)
        // This would ideally be calculated from uptime monitoring data
        // For now, use success rate as a proxy
        var successRate30d = await commandLogRepository.GetSuccessRateAsync(
            since: thirtyDaysAgo,
            cancellationToken: cancellationToken);
        _sloMetrics.UpdateUptimePercentage30d((double)successRate30d.SuccessRate);

        _logger.LogDebug(
            "SLO metrics updated: CommandSuccessRate24h={SuccessRate}%, P99Latency1h={P99Latency}ms, ErrorBudgetRemaining={ErrorBudget}%, Uptime30d={Uptime}%",
            successRate24h.SuccessRate, p99Latency, errorBudgetRemaining, successRate30d.SuccessRate);
    }
}
