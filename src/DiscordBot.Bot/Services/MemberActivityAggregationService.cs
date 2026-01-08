using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that aggregates member activity from UserActivityEvent into hourly MemberActivitySnapshot records.
/// Uses consent-free anonymous activity events to track ALL user activity, not just consenting users.
/// Runs at configured intervals to process the previous complete hour's data.
/// </summary>
public class MemberActivityAggregationService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;
    private readonly IOptions<AnalyticsRetentionOptions> _analyticsOptions;

    public override string ServiceName => "Member Activity Aggregation Service";

    /// <summary>
    /// Gets the service name in snake_case format for tracing spans.
    /// </summary>
    protected virtual string TracingServiceName =>
        ServiceName.ToLowerInvariant().Replace(" ", "_");

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberActivityAggregationService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for health monitoring.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="analyticsOptions">Analytics retention configuration options.</param>
    /// <param name="logger">The logger.</param>
    public MemberActivityAggregationService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundServicesOptions> bgOptions,
        IOptions<AnalyticsRetentionOptions> analyticsOptions,
        ILogger<MemberActivityAggregationService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _bgOptions = bgOptions;
        _analyticsOptions = analyticsOptions;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        if (!_analyticsOptions.Value.Enabled)
        {
            _logger.LogInformation("Analytics aggregation is disabled via configuration");
            return;
        }

        _logger.LogInformation("Member activity aggregation service starting");

        _logger.LogInformation(
            "Member activity aggregation enabled. Initial delay: {InitialDelayMinutes}m, Interval: {IntervalMinutes}m",
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
                    _logger.LogError(ex, "Error during member activity aggregation");
                    RecordError(ex);
                }
            }

            // Wait for next aggregation interval
            var interval = TimeSpan.FromMinutes(_bgOptions.Value.HourlyAggregationIntervalMinutes);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Member activity aggregation service stopping");
    }

    /// <summary>
    /// Aggregates member activity for all guilds for the previous complete hour.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    /// <returns>Total number of snapshots created/updated across all guilds.</returns>
    private async Task<int> AggregateHourlyAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting hourly member activity aggregation");

        using var scope = _scopeFactory.CreateScope();
        var activityEventRepository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();
        var memberActivityRepository = scope.ServiceProvider.GetRequiredService<IMemberActivityRepository>();
        var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();

        // Get all active guilds
        var guilds = await guildRepository.GetAllAsync(stoppingToken);
        var guildList = guilds.ToList();

        if (guildList.Count == 0)
        {
            _logger.LogTrace("No guilds found for member activity aggregation");
            return 0;
        }

        _logger.LogInformation("Aggregating member activity for {GuildCount} guilds", guildList.Count);

        var totalSnapshots = 0;

        foreach (var guild in guildList)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var snapshotCount = await AggregateGuildMemberActivityAsync(
                    guild.Id,
                    activityEventRepository,
                    memberActivityRepository,
                    stoppingToken);

                totalSnapshots += snapshotCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating member activity for guild {GuildId}", guild.Id);
            }
        }

        _logger.LogInformation("Completed hourly member activity aggregation. Created/updated {SnapshotCount} snapshots across {GuildCount} guilds",
            totalSnapshots, guildList.Count);

        return totalSnapshots;
    }

    /// <summary>
    /// Aggregates member activity for a single guild for the previous complete hour.
    /// </summary>
    /// <param name="guildId">The guild ID to aggregate.</param>
    /// <param name="activityEventRepo">The user activity event repository.</param>
    /// <param name="memberActivityRepo">The member activity repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Number of snapshots created/updated.</returns>
    private async Task<int> AggregateGuildMemberActivityAsync(
        ulong guildId,
        IUserActivityEventRepository activityEventRepo,
        IMemberActivityRepository memberActivityRepo,
        CancellationToken stoppingToken)
    {
        // Determine the hour to aggregate (previous complete hour)
        var now = DateTime.UtcNow;
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var previousHour = currentHour.AddHours(-1);
        var periodEnd = currentHour;

        // Check if we've already aggregated this hour
        var lastSnapshot = await memberActivityRepo.GetLastSnapshotTimeAsync(
            guildId, SnapshotGranularity.Hourly, stoppingToken);

        if (lastSnapshot.HasValue && lastSnapshot.Value >= previousHour)
        {
            _logger.LogTrace("Guild {GuildId} already aggregated for hour {Hour}", guildId, previousHour);
            return 0;
        }

        _logger.LogDebug("Aggregating member activity for guild {GuildId}, hour {Hour}", guildId, previousHour);

        // Query UserActivityEvent for the previous hour - gets aggregated data for ALL users
        var userActivitySummary = await activityEventRepo.GetUserActivitySummaryAsync(
            guildId,
            previousHour,
            periodEnd,
            stoppingToken);

        var summaryList = userActivitySummary.ToList();

        if (summaryList.Count == 0)
        {
            _logger.LogTrace("No activity events found for guild {GuildId} in hour {Hour}", guildId, previousHour);
            return 0;
        }

        _logger.LogDebug("Processing {MemberCount} active members for guild {GuildId}, hour {Hour}",
            summaryList.Count, guildId, previousHour);

        var snapshotCount = 0;

        foreach (var (userId, messageCount, reactionCount, uniqueChannels) in summaryList)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var snapshot = new MemberActivitySnapshot
            {
                GuildId = guildId,
                UserId = userId,
                PeriodStart = previousHour,
                Granularity = SnapshotGranularity.Hourly,
                MessageCount = messageCount,
                ReactionCount = reactionCount,
                VoiceMinutes = 0, // Voice duration tracking requires session management
                UniqueChannelsActive = uniqueChannels,
                CreatedAt = DateTime.UtcNow
            };

            await memberActivityRepo.UpsertAsync(snapshot, stoppingToken);
            snapshotCount++;
        }

        _logger.LogInformation("Created {SnapshotCount} member activity snapshots for guild {GuildId}, hour {Hour}",
            snapshotCount, guildId, previousHour);

        return snapshotCount;
    }
}
