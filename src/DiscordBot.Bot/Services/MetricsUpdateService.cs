using Discord.WebSocket;
using DiscordBot.Bot.Metrics;
using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically updates observable gauge metrics.
/// Updates metrics such as active guild count and estimated unique user count.
/// </summary>
public class MetricsUpdateService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly BotMetrics _botMetrics;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;
    private readonly ILogger<MetricsUpdateService> _logger;

    public MetricsUpdateService(
        DiscordSocketClient client,
        BotMetrics botMetrics,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<MetricsUpdateService> logger)
    {
        _client = client;
        _botMetrics = botMetrics;
        _bgOptions = bgOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var updateInterval = TimeSpan.FromSeconds(_bgOptions.Value.MetricsUpdateIntervalSeconds);

        _logger.LogInformation("Metrics update service starting, will update every {Interval} seconds", updateInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                UpdateMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics");
            }

            await Task.Delay(updateInterval, stoppingToken);
        }

        _logger.LogInformation("Metrics update service stopping");
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

        _logger.LogTrace(
            "Updated metrics: Guilds={GuildCount}, EstimatedUsers={UserCount}",
            guildCount,
            estimatedUsers);
    }
}
