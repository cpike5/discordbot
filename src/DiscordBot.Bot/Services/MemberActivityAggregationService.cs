using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that aggregates member activity from MessageLog into hourly MemberActivitySnapshot records.
/// Runs at configured intervals to process the previous complete hour's data.
/// </summary>
public class MemberActivityAggregationService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;
    private readonly IOptions<AnalyticsRetentionOptions> _analyticsOptions;

    public override string ServiceName => "Member Activity Aggregation Service";

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

        while (!stoppingToken.IsCancellationRequested)
        {
            UpdateHeartbeat();

            try
            {
                await AggregateHourlyAsync(stoppingToken);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during member activity aggregation");
                RecordError(ex);
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
    private async Task AggregateHourlyAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting hourly member activity aggregation");

        using var scope = _scopeFactory.CreateScope();
        var messageLogRepository = scope.ServiceProvider.GetRequiredService<IMessageLogRepository>();
        var memberActivityRepository = scope.ServiceProvider.GetRequiredService<IMemberActivityRepository>();
        var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();

        // Get all active guilds
        var guilds = await guildRepository.GetAllAsync(stoppingToken);
        var guildList = guilds.ToList();

        if (guildList.Count == 0)
        {
            _logger.LogTrace("No guilds found for member activity aggregation");
            return;
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
                    messageLogRepository,
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
    }

    /// <summary>
    /// Aggregates member activity for a single guild for the previous complete hour.
    /// </summary>
    /// <param name="guildId">The guild ID to aggregate.</param>
    /// <param name="messageLogRepo">The message log repository.</param>
    /// <param name="memberActivityRepo">The member activity repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Number of snapshots created/updated.</returns>
    private async Task<int> AggregateGuildMemberActivityAsync(
        ulong guildId,
        IMessageLogRepository messageLogRepo,
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

        // Query MessageLog for the previous hour
        var messages = await messageLogRepo.GetGuildMessagesAsync(
            guildId,
            since: previousHour,
            limit: int.MaxValue,
            cancellationToken: stoppingToken);

        var messageList = messages
            .Where(m => m.Timestamp >= previousHour && m.Timestamp < periodEnd)
            .ToList();

        if (messageList.Count == 0)
        {
            _logger.LogTrace("No messages found for guild {GuildId} in hour {Hour}", guildId, previousHour);
            return 0;
        }

        // Group by AuthorId and compute aggregates
        var memberGroups = messageList
            .GroupBy(m => m.AuthorId)
            .ToList();

        _logger.LogDebug("Processing {MemberCount} active members for guild {GuildId}, hour {Hour}",
            memberGroups.Count, guildId, previousHour);

        var snapshotCount = 0;

        foreach (var memberGroup in memberGroups)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var memberMessages = memberGroup.ToList();
            var uniqueChannels = memberMessages.Select(m => m.ChannelId).Distinct().Count();

            var snapshot = new MemberActivitySnapshot
            {
                GuildId = guildId,
                UserId = memberGroup.Key,
                PeriodStart = previousHour,
                Granularity = SnapshotGranularity.Hourly,
                MessageCount = memberMessages.Count,
                ReactionCount = 0, // Event tracking not yet implemented
                VoiceMinutes = 0, // Event tracking not yet implemented
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
