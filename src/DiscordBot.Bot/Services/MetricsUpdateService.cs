using Discord.WebSocket;
using DiscordBot.Bot.Metrics;
using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically updates observable gauge metrics.
/// Updates metrics such as active guild count and estimated unique user count.
/// </summary>
public class MetricsUpdateService : MonitoredBackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly BotMetrics _botMetrics;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "MetricsUpdateService";

    public MetricsUpdateService(
        DiscordSocketClient client,
        BotMetrics botMetrics,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<MetricsUpdateService> logger,
        IServiceProvider serviceProvider)
        : base(serviceProvider, logger)
    {
        _client = client;
        _botMetrics = botMetrics;
        _bgOptions = bgOptions;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var updateInterval = TimeSpan.FromSeconds(_bgOptions.Value.MetricsUpdateIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            UpdateHeartbeat();
            try
            {
                UpdateMetrics();
                ClearError();
            }
            catch (Exception ex)
            {
                RecordError(ex);
            }

            await Task.Delay(updateInterval, stoppingToken);
        }
    }

    private void UpdateMetrics()
    {
        // Update active guild count
        var guildCount = _client.Guilds.Count;
        _botMetrics.UpdateActiveGuildCount(guildCount);

        // Estimate unique users (sum of all guild member counts, may have duplicates)
        // For accurate count, you'd need to track unique user IDs across all guilds
        var estimatedUsers = _client.Guilds.Sum(g => g.MemberCount);
        _botMetrics.UpdateUniqueUserCount(estimatedUsers);
    }
}
