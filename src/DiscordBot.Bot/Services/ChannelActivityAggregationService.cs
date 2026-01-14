using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that aggregates channel activity from UserActivityEvent into hourly ChannelActivitySnapshot records.
/// Runs at configured intervals to process the previous complete hour's data.
/// Uses consent-free analytics events to include all user activity regardless of MessageLogging consent.
/// </summary>
public class ChannelActivityAggregationService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;
    private readonly IOptions<AnalyticsRetentionOptions> _analyticsOptions;
    private readonly DiscordSocketClient _client;

    public override string ServiceName => "Channel Activity Aggregation Service";

    /// <summary>
    /// Gets the service name in snake_case format for tracing spans.
    /// </summary>
    protected virtual string TracingServiceName =>
        ServiceName.ToLowerInvariant().Replace(" ", "_");

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelActivityAggregationService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for health monitoring.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="analyticsOptions">Analytics retention configuration options.</param>
    /// <param name="client">The Discord socket client for retrieving channel names.</param>
    /// <param name="logger">The logger.</param>
    public ChannelActivityAggregationService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundServicesOptions> bgOptions,
        IOptions<AnalyticsRetentionOptions> analyticsOptions,
        DiscordSocketClient client,
        ILogger<ChannelActivityAggregationService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _bgOptions = bgOptions;
        _analyticsOptions = analyticsOptions;
        _client = client;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        if (!_analyticsOptions.Value.Enabled)
        {
            _logger.LogInformation("Analytics aggregation is disabled via configuration");
            return;
        }

        _logger.LogInformation("Channel activity aggregation service starting");

        _logger.LogInformation(
            "Channel activity aggregation enabled. Initial delay: {InitialDelayMinutes}m, Interval: {IntervalMinutes}m",
            _bgOptions.Value.AnalyticsAggregationInitialDelayMinutes,
            _bgOptions.Value.HourlyAggregationIntervalMinutes);

        // Initial delay to let the app start up
        var initialDelay = TimeSpan.FromMinutes(_bgOptions.Value.AnalyticsAggregationInitialDelayMinutes);
        await Task.Delay(initialDelay, stoppingToken);

        var executionCycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            executionCycle++;
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            using (var activity = BotActivitySource.StartBackgroundServiceActivity(
                TracingServiceName,
                executionCycle,
                correlationId))
            {
                UpdateHeartbeat();

                try
                {
                    var snapshotsProcessed = await AggregateHourlyAsync(stoppingToken);

                    BotActivitySource.SetRecordsProcessed(activity, snapshotsProcessed);
                    BotActivitySource.SetSuccess(activity);
                    ClearError();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Normal shutdown
                    break;
                }
                catch (Exception ex)
                {
                    BotActivitySource.RecordException(activity, ex);
                    _logger.LogError(ex, "Error during channel activity aggregation");
                    RecordError(ex);
                }
            }

            // Wait for next aggregation interval
            var interval = TimeSpan.FromMinutes(_bgOptions.Value.HourlyAggregationIntervalMinutes);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Channel activity aggregation service stopping");
    }

    /// <summary>
    /// Aggregates channel activity for all guilds for the previous complete hour.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    /// <returns>Total number of snapshots created/updated across all guilds.</returns>
    private async Task<int> AggregateHourlyAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting hourly channel activity aggregation");

        using var scope = _scopeFactory.CreateScope();
        var userActivityEventRepository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();
        var channelActivityRepository = scope.ServiceProvider.GetRequiredService<IChannelActivityRepository>();
        var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();

        // Get all active guilds
        var guilds = await guildRepository.GetAllAsync(stoppingToken);
        var guildList = guilds.ToList();

        if (guildList.Count == 0)
        {
            _logger.LogTrace("No guilds found for channel activity aggregation");
            return 0;
        }

        _logger.LogInformation("Aggregating channel activity for {GuildCount} guilds", guildList.Count);

        var totalSnapshots = 0;

        foreach (var guild in guildList)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var snapshotCount = await AggregateGuildChannelActivityAsync(
                    guild.Id,
                    userActivityEventRepository,
                    channelActivityRepository,
                    stoppingToken);

                totalSnapshots += snapshotCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating channel activity for guild {GuildId}", guild.Id);
            }
        }

        _logger.LogInformation("Completed hourly channel activity aggregation. Created/updated {SnapshotCount} snapshots across {GuildCount} guilds",
            totalSnapshots, guildList.Count);

        return totalSnapshots;
    }

    /// <summary>
    /// Aggregates channel activity for a single guild for the previous complete hour.
    /// </summary>
    /// <param name="guildId">The guild ID to aggregate.</param>
    /// <param name="userActivityEventRepo">The user activity event repository.</param>
    /// <param name="channelActivityRepo">The channel activity repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Number of snapshots created/updated.</returns>
    private async Task<int> AggregateGuildChannelActivityAsync(
        ulong guildId,
        IUserActivityEventRepository userActivityEventRepo,
        IChannelActivityRepository channelActivityRepo,
        CancellationToken stoppingToken)
    {
        // Determine the hour to aggregate (previous complete hour)
        var now = DateTime.UtcNow;
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var previousHour = currentHour.AddHours(-1);
        var periodEnd = currentHour;

        _logger.LogDebug("Aggregating channel activity for guild {GuildId}, hour {Hour}", guildId, previousHour);

        // Query UserActivityEvent for the previous hour (Message events only)
        var activityEvents = await userActivityEventRepo.GetByGuildAsync(
            guildId,
            since: previousHour,
            until: periodEnd,
            cancellationToken: stoppingToken);

        var messageEvents = activityEvents
            .Where(e => e.Timestamp >= previousHour && e.Timestamp < periodEnd)
            .Where(e => e.EventType == ActivityEventType.Message)
            .ToList();

        if (messageEvents.Count == 0)
        {
            _logger.LogTrace("No message events found for guild {GuildId} in hour {Hour}", guildId, previousHour);
            return 0;
        }

        // Group by ChannelId and compute aggregates
        var channelGroups = messageEvents
            .GroupBy(e => e.ChannelId)
            .ToList();

        _logger.LogDebug("Processing {ChannelCount} active channels for guild {GuildId}, hour {Hour}",
            channelGroups.Count, guildId, previousHour);

        var snapshotCount = 0;

        foreach (var channelGroup in channelGroups)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var channelEvents = channelGroup.ToList();
            var uniqueUsers = channelEvents.Select(e => e.UserId).Distinct().Count();

            // Note: UserActivityEvent doesn't store content, so AverageMessageLength is unavailable
            // This is a trade-off for consent-free analytics (no message content stored)
            var averageMessageLength = 0.0;

            // Try to get channel name from Discord client
            var channelName = await GetChannelNameAsync(guildId, channelGroup.Key);

            var snapshot = new ChannelActivitySnapshot
            {
                GuildId = guildId,
                ChannelId = channelGroup.Key,
                ChannelName = channelName,
                PeriodStart = previousHour,
                Granularity = SnapshotGranularity.Hourly,
                MessageCount = channelEvents.Count,
                UniqueUsers = uniqueUsers,
                PeakHour = null, // Null for hourly snapshots
                PeakHourMessageCount = null,
                AverageMessageLength = averageMessageLength,
                CreatedAt = DateTime.UtcNow
            };

            await channelActivityRepo.UpsertAsync(snapshot, stoppingToken);
            snapshotCount++;
        }

        _logger.LogInformation("Created {SnapshotCount} channel activity snapshots for guild {GuildId}, hour {Hour}",
            snapshotCount, guildId, previousHour);

        return snapshotCount;
    }

    /// <summary>
    /// Gets the channel name from Discord, with fallback to "Unknown Channel".
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>Channel name or "Unknown Channel" if not found.</returns>
    private async Task<string> GetChannelNameAsync(ulong guildId, ulong channelId)
    {
        try
        {
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogTrace("Guild {GuildId} not found in cache for channel name lookup", guildId);
                return "Unknown Channel";
            }

            var channel = guild.GetChannel(channelId);
            if (channel != null)
            {
                return channel.Name;
            }

            _logger.LogTrace("Channel {ChannelId} not found in guild {GuildId}", channelId, guildId);
            return "Unknown Channel";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving channel name for {ChannelId} in guild {GuildId}", channelId, guildId);
            return "Unknown Channel";
        }
    }
}
