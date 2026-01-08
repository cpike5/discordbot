using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that aggregates channel activity from UserActivityEvent into hourly ChannelActivitySnapshot records.
/// Uses consent-free anonymous activity events to track ALL channel activity, not just from consenting users.
/// Runs at configured intervals to process the previous complete hour's data.
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
        var activityEventRepository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();
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
                    activityEventRepository,
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
    /// <param name="activityEventRepo">The user activity event repository.</param>
    /// <param name="channelActivityRepo">The channel activity repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Number of snapshots created/updated.</returns>
    private async Task<int> AggregateGuildChannelActivityAsync(
        ulong guildId,
        IUserActivityEventRepository activityEventRepo,
        IChannelActivityRepository channelActivityRepo,
        CancellationToken stoppingToken)
    {
        // Determine the hour to aggregate (previous complete hour)
        var now = DateTime.UtcNow;
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var previousHour = currentHour.AddHours(-1);
        var periodEnd = currentHour;

        _logger.LogDebug("Aggregating channel activity for guild {GuildId}, hour {Hour}", guildId, previousHour);

        // Query UserActivityEvent for the previous hour - gets aggregated data for ALL channels
        var channelActivitySummary = await activityEventRepo.GetChannelActivitySummaryAsync(
            guildId,
            previousHour,
            periodEnd,
            stoppingToken);

        var summaryList = channelActivitySummary.ToList();

        if (summaryList.Count == 0)
        {
            _logger.LogTrace("No activity events found for guild {GuildId} in hour {Hour}", guildId, previousHour);
            return 0;
        }

        _logger.LogDebug("Processing {ChannelCount} active channels for guild {GuildId}, hour {Hour}",
            summaryList.Count, guildId, previousHour);

        var snapshotCount = 0;

        foreach (var (channelId, messageCount, reactionCount, uniqueUsers) in summaryList)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // Try to get channel name from Discord client
            var channelName = await GetChannelNameAsync(guildId, channelId);

            var snapshot = new ChannelActivitySnapshot
            {
                GuildId = guildId,
                ChannelId = channelId,
                ChannelName = channelName,
                PeriodStart = previousHour,
                Granularity = SnapshotGranularity.Hourly,
                MessageCount = messageCount,
                UniqueUsers = uniqueUsers,
                PeakHour = null, // Null for hourly snapshots
                PeakHourMessageCount = null,
                AverageMessageLength = 0.0, // Content not available with anonymous events
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
