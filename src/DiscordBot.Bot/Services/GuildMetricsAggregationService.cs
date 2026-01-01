using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that aggregates daily guild-level metrics into GuildMetricsSnapshot records.
/// Runs at a configured UTC hour each day to process the previous day's data.
/// </summary>
public class GuildMetricsAggregationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;
    private readonly IOptions<AnalyticsRetentionOptions> _analyticsOptions;
    private readonly ILogger<GuildMetricsAggregationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildMetricsAggregationService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="analyticsOptions">Analytics retention configuration options.</param>
    /// <param name="logger">The logger.</param>
    public GuildMetricsAggregationService(
        IServiceScopeFactory scopeFactory,
        IOptions<BackgroundServicesOptions> bgOptions,
        IOptions<AnalyticsRetentionOptions> analyticsOptions,
        ILogger<GuildMetricsAggregationService> logger)
    {
        _scopeFactory = scopeFactory;
        _bgOptions = bgOptions;
        _analyticsOptions = analyticsOptions;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_analyticsOptions.Value.Enabled)
        {
            _logger.LogInformation("Analytics aggregation is disabled via configuration");
            return;
        }

        _logger.LogInformation("Guild metrics aggregation service starting");

        _logger.LogInformation(
            "Guild metrics aggregation enabled. Initial delay: {InitialDelayMinutes}m, Interval: {IntervalMinutes}m, Target hour: {TargetHour} UTC",
            _bgOptions.Value.AnalyticsAggregationInitialDelayMinutes,
            _bgOptions.Value.DailyAggregationIntervalMinutes,
            _bgOptions.Value.DailyAggregationHourUtc);

        // Initial delay to let the app start up
        var initialDelay = TimeSpan.FromMinutes(_bgOptions.Value.AnalyticsAggregationInitialDelayMinutes);
        await Task.Delay(initialDelay, stoppingToken);

        // Calculate time until next target hour
        var nextRun = CalculateNextRunTime();
        var delayUntilNextRun = nextRun - DateTime.UtcNow;

        if (delayUntilNextRun > TimeSpan.Zero)
        {
            _logger.LogInformation("Waiting until {NextRun} UTC for first daily aggregation", nextRun);
            await Task.Delay(delayUntilNextRun, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AggregateDailyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during guild metrics aggregation");
            }

            // Wait for next aggregation interval (typically 24 hours)
            var interval = TimeSpan.FromMinutes(_bgOptions.Value.DailyAggregationIntervalMinutes);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Guild metrics aggregation service stopping");
    }

    /// <summary>
    /// Calculates the next run time based on the configured UTC hour.
    /// </summary>
    /// <returns>The next scheduled run time in UTC.</returns>
    private DateTime CalculateNextRunTime()
    {
        var now = DateTime.UtcNow;
        var targetHour = _bgOptions.Value.DailyAggregationHourUtc;

        var today = new DateTime(now.Year, now.Month, now.Day, targetHour, 0, 0, DateTimeKind.Utc);

        if (now < today)
        {
            return today;
        }
        else
        {
            return today.AddDays(1);
        }
    }

    /// <summary>
    /// Aggregates daily guild metrics for all guilds for the previous complete day.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task AggregateDailyAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting daily guild metrics aggregation");

        using var scope = _scopeFactory.CreateScope();
        var messageLogRepository = scope.ServiceProvider.GetRequiredService<IMessageLogRepository>();
        var commandLogRepository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();
        var moderationCaseRepository = scope.ServiceProvider.GetRequiredService<IModerationCaseRepository>();
        var guildMemberRepository = scope.ServiceProvider.GetRequiredService<IGuildMemberRepository>();
        var memberActivityRepository = scope.ServiceProvider.GetRequiredService<IMemberActivityRepository>();
        var guildMetricsRepository = scope.ServiceProvider.GetRequiredService<IGuildMetricsRepository>();
        var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();

        // Get all active guilds
        var guilds = await guildRepository.GetAllAsync(stoppingToken);
        var guildList = guilds.ToList();

        if (guildList.Count == 0)
        {
            _logger.LogTrace("No guilds found for guild metrics aggregation");
            return;
        }

        _logger.LogInformation("Aggregating daily metrics for {GuildCount} guilds", guildList.Count);

        var totalSnapshots = 0;

        foreach (var guild in guildList)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var created = await AggregateGuildDailyMetricsAsync(
                    guild.Id,
                    messageLogRepository,
                    commandLogRepository,
                    moderationCaseRepository,
                    guildMemberRepository,
                    memberActivityRepository,
                    guildMetricsRepository,
                    stoppingToken);

                if (created)
                {
                    totalSnapshots++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating daily metrics for guild {GuildId}", guild.Id);
            }
        }

        _logger.LogInformation("Completed daily guild metrics aggregation. Created/updated {SnapshotCount} snapshots across {GuildCount} guilds",
            totalSnapshots, guildList.Count);
    }

    /// <summary>
    /// Aggregates daily metrics for a single guild for the previous complete day.
    /// </summary>
    /// <param name="guildId">The guild ID to aggregate.</param>
    /// <param name="messageLogRepo">The message log repository.</param>
    /// <param name="commandLogRepo">The command log repository.</param>
    /// <param name="moderationCaseRepo">The moderation case repository.</param>
    /// <param name="guildMemberRepo">The guild member repository.</param>
    /// <param name="memberActivityRepo">The member activity repository.</param>
    /// <param name="guildMetricsRepo">The guild metrics repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>True if a snapshot was created/updated, false otherwise.</returns>
    private async Task<bool> AggregateGuildDailyMetricsAsync(
        ulong guildId,
        IMessageLogRepository messageLogRepo,
        ICommandLogRepository commandLogRepo,
        IModerationCaseRepository moderationCaseRepo,
        IGuildMemberRepository guildMemberRepo,
        IMemberActivityRepository memberActivityRepo,
        IGuildMetricsRepository guildMetricsRepo,
        CancellationToken stoppingToken)
    {
        // Determine the day to aggregate (previous complete day)
        var now = DateTime.UtcNow;
        var yesterday = now.Date.AddDays(-1);
        var snapshotDate = DateOnly.FromDateTime(yesterday);
        var dayStart = yesterday;
        var dayEnd = yesterday.AddDays(1);

        // Check if we've already aggregated this day
        var existingSnapshot = await guildMetricsRepo.GetByDateRangeAsync(
            guildId, snapshotDate, snapshotDate, stoppingToken);

        if (existingSnapshot.Any())
        {
            _logger.LogTrace("Guild {GuildId} already aggregated for date {Date}", guildId, snapshotDate);
            return false;
        }

        _logger.LogDebug("Aggregating daily metrics for guild {GuildId}, date {Date}", guildId, snapshotDate);

        // Get total member count (current)
        var totalMembers = await guildMemberRepo.GetMemberCountAsync(guildId, activeOnly: true, stoppingToken);

        // Get active members (those with at least one message)
        var activeMembers = await memberActivityRepo.GetActivityTimeSeriesAsync(
            guildId,
            dayStart,
            dayEnd,
            SnapshotGranularity.Hourly,
            stoppingToken);

        var activeMemberCount = activeMembers
            .Select(a => a.ActiveMembers)
            .DefaultIfEmpty(0)
            .Sum();

        // Get total messages
        var messages = await messageLogRepo.GetGuildMessagesAsync(
            guildId,
            since: dayStart,
            limit: int.MaxValue,
            cancellationToken: stoppingToken);

        var totalMessages = messages
            .Count(m => m.Timestamp >= dayStart && m.Timestamp < dayEnd);

        // Get commands executed
        var commandsByGuild = await commandLogRepo.GetCommandCountsByGuildAsync(dayStart, stoppingToken);
        var commandsExecuted = commandsByGuild.TryGetValue(guildId, out var count) ? count : 0;

        // Get moderation actions
        var moderationCases = await moderationCaseRepo.GetByGuildAsync(
            guildId,
            startDate: dayStart,
            endDate: dayEnd,
            cancellationToken: stoppingToken);
        var moderationActions = moderationCases.Items.Count();

        // Get active channels
        var activeChannels = messages
            .Where(m => m.Timestamp >= dayStart && m.Timestamp < dayEnd)
            .Select(m => m.ChannelId)
            .Distinct()
            .Count();

        // Calculate member joins/leaves (estimate from current data vs previous snapshot)
        var previousSnapshot = await guildMetricsRepo.GetLatestAsync(guildId, stoppingToken);
        var membersJoined = 0;
        var membersLeft = 0;

        if (previousSnapshot != null)
        {
            var netChange = totalMembers - previousSnapshot.TotalMembers;
            if (netChange > 0)
            {
                membersJoined = netChange;
            }
            else if (netChange < 0)
            {
                membersLeft = Math.Abs(netChange);
            }
        }

        var snapshot = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = snapshotDate,
            TotalMembers = totalMembers,
            ActiveMembers = activeMemberCount,
            MembersJoined = membersJoined,
            MembersLeft = membersLeft,
            TotalMessages = totalMessages,
            CommandsExecuted = commandsExecuted,
            ModerationActions = moderationActions,
            ActiveChannels = activeChannels,
            TotalVoiceMinutes = 0, // Voice tracking not yet implemented
            CreatedAt = DateTime.UtcNow
        };

        await guildMetricsRepo.UpsertAsync(snapshot, stoppingToken);

        _logger.LogInformation("Created daily metrics snapshot for guild {GuildId}, date {Date}: {TotalMembers} members, {TotalMessages} messages, {CommandsExecuted} commands",
            guildId, snapshotDate, totalMembers, totalMessages, commandsExecuted);

        return true;
    }
}
