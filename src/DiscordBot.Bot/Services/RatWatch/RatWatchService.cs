using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Services.RatWatch;

/// <summary>
/// Service implementation for managing Rat Watch accountability trackers.
/// Handles watch creation, voting, execution, and statistics.
/// </summary>
public class RatWatchService : IRatWatchService
{
    private readonly IRatWatchRepository _watchRepository;
    private readonly IRatVoteRepository _voteRepository;
    private readonly IRatRecordRepository _recordRepository;
    private readonly IGuildRatWatchSettingsRepository _settingsRepository;
    private readonly DiscordSocketClient _client;
    private readonly IRatWatchStatusService _ratWatchStatusService;
    private readonly ILogger<RatWatchService> _logger;
    private readonly RatWatchOptions _options;

    public RatWatchService(
        IRatWatchRepository watchRepository,
        IRatVoteRepository voteRepository,
        IRatRecordRepository recordRepository,
        IGuildRatWatchSettingsRepository settingsRepository,
        DiscordSocketClient client,
        IRatWatchStatusService ratWatchStatusService,
        ILogger<RatWatchService> logger,
        IOptions<RatWatchOptions> options)
    {
        _watchRepository = watchRepository;
        _voteRepository = voteRepository;
        _recordRepository = recordRepository;
        _settingsRepository = settingsRepository;
        _client = client;
        _ratWatchStatusService = ratWatchStatusService;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<RatWatchDto> CreateWatchAsync(RatWatchCreateDto dto, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "create_watch",
            guildId: dto.GuildId,
            userId: dto.AccusedUserId);

        try
        {
            _logger.LogInformation("Creating Rat Watch for user {AccusedUserId} in guild {GuildId}, scheduled at {ScheduledAt}",
                dto.AccusedUserId, dto.GuildId, dto.ScheduledAt);

            // Check for duplicate watches
            var duplicate = await _watchRepository.FindDuplicateAsync(
                dto.GuildId, dto.AccusedUserId, dto.ScheduledAt, ct);

            if (duplicate != null)
            {
                _logger.LogWarning("Duplicate Rat Watch found for user {AccusedUserId} at {ScheduledAt}",
                    dto.AccusedUserId, dto.ScheduledAt);
                throw new InvalidOperationException("A watch already exists for this user at this time.");
            }

            var now = DateTime.UtcNow;
            var watch = new Core.Entities.RatWatch
            {
                Id = Guid.NewGuid(),
                GuildId = dto.GuildId,
                ChannelId = dto.ChannelId,
                AccusedUserId = dto.AccusedUserId,
                InitiatorUserId = dto.InitiatorUserId,
                OriginalMessageId = dto.OriginalMessageId,
                CustomMessage = dto.CustomMessage,
                ScheduledAt = dto.ScheduledAt,
                CreatedAt = now,
                Status = RatWatchStatus.Pending
            };

            await _watchRepository.AddAsync(watch, ct);

            _logger.LogInformation("Rat Watch {WatchId} created successfully for user {AccusedUserId}",
                watch.Id, dto.AccusedUserId);

            var result = await MapToDtoAsync(watch, ct);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<RatWatchDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_by_id",
            entityId: id.ToString());

        try
        {
            _logger.LogDebug("Retrieving Rat Watch {WatchId}", id);

            var watch = await _watchRepository.GetByIdWithVotesAsync(id, ct);
            if (watch == null)
            {
                _logger.LogWarning("Rat Watch {WatchId} not found", id);
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            var result = await MapToDtoAsync(watch, ct);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<RatWatchDto> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_by_guild",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Retrieving Rat Watches for guild {GuildId}, page {Page}, pageSize {PageSize}",
                guildId, page, pageSize);

            var (watches, totalCount) = await _watchRepository.GetByGuildAsync(guildId, page, pageSize, ct);
            var dtos = new List<RatWatchDto>();

            foreach (var watch in watches)
            {
                dtos.Add(await MapToDtoAsync(watch, ct));
            }

            _logger.LogInformation("Retrieved {Count} of {Total} Rat Watches for guild {GuildId}",
                dtos.Count, totalCount, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return (dtos, totalCount);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CancelWatchAsync(Guid id, string reason, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "cancel_watch",
            entityId: id.ToString());

        try
        {
            _logger.LogInformation("Cancelling Rat Watch {WatchId}, reason: {Reason}", id, reason);

            var watch = await _watchRepository.GetByIdAsync(id, ct);
            if (watch == null)
            {
                _logger.LogWarning("Rat Watch {WatchId} not found for cancellation", id);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            if (watch.Status != RatWatchStatus.Pending)
            {
                _logger.LogWarning("Cannot cancel Rat Watch {WatchId} with status {Status}", id, watch.Status);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            watch.Status = RatWatchStatus.Cancelled;
            watch.Guild = null; // Detach navigation to avoid EF tracking conflicts
            await _watchRepository.UpdateAsync(watch, ct);

            // Refresh bot status to clear "Watching for rats..." if no other active watches
            _ratWatchStatusService.RequestStatusUpdate();

            _logger.LogInformation("Rat Watch {WatchId} cancelled successfully", id);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ClearWatchAsync(Guid watchId, ulong userId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "clear_watch",
            userId: userId,
            entityId: watchId.ToString());

        try
        {
            _logger.LogInformation("User {UserId} attempting to clear Rat Watch {WatchId}", userId, watchId);

            var watch = await _watchRepository.GetByIdAsync(watchId, ct);
            if (watch == null)
            {
                _logger.LogWarning("Rat Watch {WatchId} not found for clearing", watchId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            if (watch.AccusedUserId != userId)
            {
                _logger.LogWarning("User {UserId} is not the accused user for Rat Watch {WatchId}", userId, watchId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            if (watch.Status != RatWatchStatus.Pending)
            {
                _logger.LogWarning("Cannot clear Rat Watch {WatchId} with status {Status}", watchId, watch.Status);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            watch.Status = RatWatchStatus.ClearedEarly;
            watch.ClearedAt = DateTime.UtcNow;
            watch.Guild = null; // Detach navigation to avoid EF tracking conflicts
            await _watchRepository.UpdateAsync(watch, ct);

            _logger.LogInformation("Rat Watch {WatchId} cleared early by user {UserId}", watchId, userId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CastVoteAsync(Guid watchId, ulong voterId, bool isGuilty, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "cast_vote",
            userId: voterId,
            entityId: watchId.ToString());

        try
        {
            _logger.LogDebug("User {VoterId} casting vote on Rat Watch {WatchId}: {Vote}",
                voterId, watchId, isGuilty ? "Guilty" : "Not Guilty");

            var watch = await _watchRepository.GetByIdAsync(watchId, ct);
            if (watch == null)
            {
                _logger.LogWarning("Rat Watch {WatchId} not found for voting", watchId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            if (watch.Status != RatWatchStatus.Voting)
            {
                _logger.LogWarning("Cannot vote on Rat Watch {WatchId} with status {Status}", watchId, watch.Status);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            // Check if user already voted
            var existingVote = await _voteRepository.GetUserVoteAsync(watchId, voterId, ct);

            if (existingVote != null)
            {
                // Update existing vote
                existingVote.IsGuiltyVote = isGuilty;
                existingVote.VotedAt = DateTime.UtcNow;
                await _voteRepository.UpdateAsync(existingVote, ct);

                _logger.LogInformation("User {VoterId} changed vote on Rat Watch {WatchId} to {Vote}",
                    voterId, watchId, isGuilty ? "Guilty" : "Not Guilty");
            }
            else
            {
                // Create new vote
                var vote = new RatVote
                {
                    Id = Guid.NewGuid(),
                    RatWatchId = watchId,
                    VoterUserId = voterId,
                    IsGuiltyVote = isGuilty,
                    VotedAt = DateTime.UtcNow
                };

                await _voteRepository.AddAsync(vote, ct);

                _logger.LogInformation("User {VoterId} cast vote on Rat Watch {WatchId}: {Vote}",
                    voterId, watchId, isGuilty ? "Guilty" : "Not Guilty");
            }

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(int Guilty, int NotGuilty)> GetVoteTallyAsync(Guid watchId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_vote_tally",
            entityId: watchId.ToString());

        try
        {
            _logger.LogTrace("Getting vote tally for Rat Watch {WatchId}", watchId);

            var (guiltyCount, notGuiltyCount) = await _voteRepository.GetVoteTallyAsync(watchId, ct);

            _logger.LogDebug("Vote tally for Rat Watch {WatchId}: {Guilty} guilty, {NotGuilty} not guilty",
                watchId, guiltyCount, notGuiltyCount);

            BotActivitySource.SetSuccess(activity);
            return (guiltyCount, notGuiltyCount);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<RatStatsDto> GetUserStatsAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_user_stats",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogDebug("Getting Rat Watch stats for user {UserId} in guild {GuildId}", userId, guildId);

            var guiltyCount = await _recordRepository.GetGuiltyCountAsync(guildId, userId, ct);
            var recentRecords = await _recordRepository.GetRecentRecordsAsync(guildId, userId, 5, ct);

            var username = await GetUsernameAsync(userId, guildId);

            var recordDtos = recentRecords.Select(r => new RatRecordDto
            {
                RecordedAt = r.RecordedAt,
                GuiltyVotes = r.GuiltyVotes,
                NotGuiltyVotes = r.NotGuiltyVotes,
                OriginalMessageLink = r.OriginalMessageLink
            }).ToList();

            _logger.LogInformation("User {UserId} in guild {GuildId} has {GuiltyCount} guilty verdicts",
                userId, guildId, guiltyCount);

            var result = new RatStatsDto
            {
                UserId = userId,
                Username = username,
                TotalGuiltyCount = guiltyCount,
                RecentRecords = recordDtos
            };

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RatLeaderboardEntryDto>> GetLeaderboardAsync(
        ulong guildId,
        int limit = 10,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_leaderboard",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting Rat Watch leaderboard for guild {GuildId}, limit {Limit}", guildId, limit);

            var leaderboardData = await _recordRepository.GetLeaderboardAsync(guildId, limit, ct);
            var entries = new List<RatLeaderboardEntryDto>();
            var rank = 1;

            foreach (var (userId, guiltyCount) in leaderboardData)
            {
                var username = await GetUsernameAsync(userId, guildId);

                entries.Add(new RatLeaderboardEntryDto
                {
                    Rank = rank++,
                    UserId = userId,
                    Username = username,
                    GuiltyCount = guiltyCount
                });
            }

            _logger.LogInformation("Retrieved {Count} leaderboard entries for guild {GuildId}", entries.Count, guildId);

            BotActivitySource.SetRecordsReturned(activity, entries.Count);
            BotActivitySource.SetSuccess(activity);
            return entries;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Core.Entities.RatWatch>> GetDueWatchesAsync(CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_due_watches");

        try
        {
            var now = DateTime.UtcNow;
            _logger.LogTrace("Getting due Rat Watches before {Time}", now);

            var dueWatches = await _watchRepository.GetPendingWatchesAsync(now, ct);

            _logger.LogDebug("Found {Count} due Rat Watches", dueWatches.Count());

            BotActivitySource.SetRecordsReturned(activity, dueWatches.Count());
            BotActivitySource.SetSuccess(activity);
            return dueWatches;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StartVotingAsync(Guid watchId, ulong? votingMessageId = null, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "start_voting",
            entityId: watchId.ToString());

        try
        {
            _logger.LogInformation("Starting voting for Rat Watch {WatchId}", watchId);

            var watch = await _watchRepository.GetByIdAsync(watchId, ct);
            if (watch == null)
            {
                _logger.LogWarning("Rat Watch {WatchId} not found for starting voting", watchId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            if (watch.Status != RatWatchStatus.Pending)
            {
                _logger.LogWarning("Cannot start voting for Rat Watch {WatchId} with status {Status}",
                    watchId, watch.Status);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            watch.Status = RatWatchStatus.Voting;
            watch.VotingStartedAt = DateTime.UtcNow;

            if (votingMessageId.HasValue)
            {
                watch.VotingMessageId = votingMessageId.Value;
            }

            watch.Guild = null; // Detach navigation to avoid EF tracking conflicts
            await _watchRepository.UpdateAsync(watch, ct);

            _logger.LogInformation("Voting started for Rat Watch {WatchId}", watchId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Core.Entities.RatWatch>> GetExpiredVotingAsync(CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_expired_voting");

        try
        {
            _logger.LogTrace("Getting expired voting Rat Watches");

            var now = DateTime.UtcNow;
            var allVoting = await _watchRepository.GetActiveVotingAsync(now, ct);
            var votingList = allVoting.ToList();

            if (votingList.Count == 0)
            {
                _logger.LogDebug("No active voting Rat Watches found");
                BotActivitySource.SetRecordsReturned(activity, 0);
                BotActivitySource.SetSuccess(activity);
                return [];
            }

            // Only fetch voting durations for guilds that have active voting watches
            var guildIds = votingList.Select(w => w.GuildId).Distinct();
            var votingDurations = await _settingsRepository.GetVotingDurationsForGuildsAsync(guildIds, ct);

            // Filter to only those where voting window has expired based on guild settings
            var expiredVoting = votingList.Where(w =>
            {
                if (!w.VotingStartedAt.HasValue)
                {
                    return false;
                }

                var votingDuration = votingDurations.GetValueOrDefault(w.GuildId, _options.DefaultVotingDurationMinutes);
                var votingEndTime = w.VotingStartedAt.Value.AddMinutes(votingDuration);

                return now >= votingEndTime;
            }).ToList();

            _logger.LogDebug("Found {Count} Rat Watches with expired voting", expiredVoting.Count);

            BotActivitySource.SetRecordsReturned(activity, expiredVoting.Count);
            BotActivitySource.SetSuccess(activity);
            return expiredVoting;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> FinalizeVotingAsync(Guid watchId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "finalize_voting",
            entityId: watchId.ToString());

        try
        {
            _logger.LogInformation("Finalizing voting for Rat Watch {WatchId}", watchId);

            var watch = await _watchRepository.GetByIdWithVotesAsync(watchId, ct);
            if (watch == null)
            {
                _logger.LogWarning("Rat Watch {WatchId} not found for finalizing voting", watchId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            if (watch.Status != RatWatchStatus.Voting)
            {
                _logger.LogWarning("Cannot finalize voting for Rat Watch {WatchId} with status {Status}",
                    watchId, watch.Status);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            var (guiltyCount, notGuiltyCount) = await GetVoteTallyAsync(watchId, ct);

            // Determine verdict (ties go to not guilty)
            var isGuilty = guiltyCount > notGuiltyCount;

            watch.Status = isGuilty ? RatWatchStatus.Guilty : RatWatchStatus.NotGuilty;
            watch.VotingEndedAt = DateTime.UtcNow;
            await _watchRepository.UpdateAsync(watch, ct);

            _logger.LogInformation("Rat Watch {WatchId} finalized with verdict: {Verdict} ({Guilty} guilty, {NotGuilty} not guilty)",
                watchId, watch.Status, guiltyCount, notGuiltyCount);

            // Create record if guilty
            if (isGuilty)
            {
                var messageLink = $"https://discord.com/channels/{watch.GuildId}/{watch.ChannelId}/{watch.OriginalMessageId}";

                var record = new RatRecord
                {
                    Id = Guid.NewGuid(),
                    RatWatchId = watchId,
                    GuildId = watch.GuildId,
                    UserId = watch.AccusedUserId,
                    GuiltyVotes = guiltyCount,
                    NotGuiltyVotes = notGuiltyCount,
                    RecordedAt = DateTime.UtcNow,
                    OriginalMessageLink = messageLink
                };

                await _recordRepository.AddAsync(record, ct);

                _logger.LogInformation("Created Rat Record {RecordId} for user {UserId} in guild {GuildId}",
                    record.Id, watch.AccusedUserId, watch.GuildId);
            }

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<GuildRatWatchSettings> GetGuildSettingsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_guild_settings",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting Rat Watch settings for guild {GuildId}", guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            _logger.LogDebug("Retrieved Rat Watch settings for guild {GuildId}: Enabled={Enabled}, Timezone={Timezone}",
                guildId, settings.IsEnabled, settings.Timezone);

            BotActivitySource.SetSuccess(activity);
            return settings;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<GuildRatWatchSettings> UpdateGuildSettingsAsync(
        ulong guildId,
        Action<GuildRatWatchSettings> update,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "update_guild_settings",
            guildId: guildId);

        try
        {
            _logger.LogInformation("Updating Rat Watch settings for guild {GuildId}", guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            // Log current values before update
            _logger.LogDebug(
                "Current settings for guild {GuildId}: Timezone={Timezone}, MaxAdvanceHours={MaxAdvanceHours}, VotingDurationMinutes={VotingDurationMinutes}, IsEnabled={IsEnabled}, PublicLeaderboard={PublicLeaderboard}",
                guildId, settings.Timezone, settings.MaxAdvanceHours, settings.VotingDurationMinutes, settings.IsEnabled, settings.PublicLeaderboardEnabled);

            update(settings);
            settings.UpdatedAt = DateTime.UtcNow;

            // Log new values after update
            _logger.LogDebug(
                "New settings for guild {GuildId}: Timezone={Timezone}, MaxAdvanceHours={MaxAdvanceHours}, VotingDurationMinutes={VotingDurationMinutes}, IsEnabled={IsEnabled}, PublicLeaderboard={PublicLeaderboard}",
                guildId, settings.Timezone, settings.MaxAdvanceHours, settings.VotingDurationMinutes, settings.IsEnabled, settings.PublicLeaderboardEnabled);

            await _settingsRepository.UpdateAsync(settings, ct);

            _logger.LogInformation("Rat Watch settings updated for guild {GuildId}", guildId);

            BotActivitySource.SetSuccess(activity);
            return settings;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public DateTime? ParseScheduleTime(string input, string timezone)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "parse_schedule_time");

        try
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                _logger.LogDebug("Parse schedule time failed: input is null or whitespace");
                BotActivitySource.SetSuccess(activity);
                return null;
            }

            input = input.Trim().ToLowerInvariant();

            // Try relative time first (e.g., "10m", "2h", "1h30m")
            var relativeTime = ParseRelativeTime(input);
            if (relativeTime.HasValue)
            {
                _logger.LogDebug("Parsed relative time '{Input}' to UTC: {Result}", input, relativeTime.Value);
                BotActivitySource.SetSuccess(activity);
                return relativeTime.Value;
            }

            // Try absolute time (e.g., "10pm", "22:00", "10:30pm")
            var absoluteTime = ParseAbsoluteTime(input, timezone);
            if (absoluteTime.HasValue)
            {
                _logger.LogDebug("Parsed absolute time '{Input}' with timezone '{Timezone}' to UTC: {Result}",
                    input, timezone, absoluteTime.Value);
                BotActivitySource.SetSuccess(activity);
                return absoluteTime.Value;
            }

            _logger.LogDebug("Failed to parse schedule time: {Input}", input);
            BotActivitySource.SetSuccess(activity);
            return null;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Parses relative time formats like "10m", "2h", "1h30m".
    /// </summary>
    private DateTime? ParseRelativeTime(string input)
    {
        // Pattern: optionally starts with "in ", then one or more time components
        // Examples: "10m", "2h", "1h30m", "in 10m", "in 2h 30m"
        input = input.Replace("in ", "").Trim();

        // Match patterns like: 1h, 30m, 1h30m, 2h 30m
        var pattern = @"^(?:(\d+)\s*h(?:ours?)?)?(?:\s*(\d+)\s*m(?:in(?:utes?)?)?)?$";
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        var hours = 0;
        var minutes = 0;

        if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var h))
        {
            hours = h;
        }

        if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var m))
        {
            minutes = m;
        }

        if (hours == 0 && minutes == 0)
        {
            return null;
        }

        return DateTime.UtcNow.AddHours(hours).AddMinutes(minutes);
    }

    /// <summary>
    /// Parses absolute time formats like "10pm", "22:00", "10:30pm".
    /// </summary>
    private DateTime? ParseAbsoluteTime(string input, string timezone)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid timezone '{Timezone}', using UTC", timezone);
            timeZone = TimeZoneInfo.Utc;
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var today = now.Date;

        // Try 12-hour format with am/pm (e.g., "10pm", "10:30pm")
        var pattern12Hour = @"^(\d{1,2})(?::(\d{2}))?\s*(am|pm)$";
        var match = Regex.Match(input, pattern12Hour, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out var hour) || hour < 1 || hour > 12)
            {
                return null;
            }

            var minute = 0;
            if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var m))
            {
                minute = m;
            }

            var isPm = match.Groups[3].Value.ToLowerInvariant() == "pm";

            // Convert to 24-hour format
            if (hour == 12)
            {
                hour = isPm ? 12 : 0;
            }
            else if (isPm)
            {
                hour += 12;
            }

            var localTime = new DateTime(today.Year, today.Month, today.Day, hour, minute, 0);

            // If the time has already passed today, schedule for tomorrow
            if (localTime <= now)
            {
                localTime = localTime.AddDays(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
        }

        // Try 24-hour format (e.g., "22:00", "14:30")
        var pattern24Hour = @"^(\d{1,2}):(\d{2})$";
        match = Regex.Match(input, pattern24Hour);

        if (match.Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out var hour) || hour < 0 || hour > 23)
            {
                return null;
            }

            if (!int.TryParse(match.Groups[2].Value, out var minute) || minute < 0 || minute > 59)
            {
                return null;
            }

            var localTime = new DateTime(today.Year, today.Month, today.Day, hour, minute, 0);

            // If the time has already passed today, schedule for tomorrow
            if (localTime <= now)
            {
                localTime = localTime.AddDays(1);
            }

            return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
        }

        return null;
    }

    /// <summary>
    /// Maps a RatWatch entity to a RatWatchDto.
    /// Resolves usernames and guild name from Discord and calculates vote tallies.
    /// </summary>
    private async Task<RatWatchDto> MapToDtoAsync(Core.Entities.RatWatch watch, CancellationToken ct)
    {
        var accusedUsername = await GetUsernameAsync(watch.AccusedUserId, watch.GuildId);
        var initiatorUsername = await GetUsernameAsync(watch.InitiatorUserId, watch.GuildId);
        var guildName = GetGuildName(watch.GuildId, watch.Guild?.Name);

        // Use pre-loaded votes if available to avoid additional DB query
        int guiltyVotes, notGuiltyVotes;
        if (watch.Votes != null && watch.Votes.Count > 0)
        {
            guiltyVotes = watch.Votes.Count(v => v.IsGuiltyVote);
            notGuiltyVotes = watch.Votes.Count(v => !v.IsGuiltyVote);
        }
        else if (watch.Votes != null)
        {
            // Votes loaded but empty
            guiltyVotes = 0;
            notGuiltyVotes = 0;
        }
        else
        {
            // Votes not loaded, fetch from database
            (guiltyVotes, notGuiltyVotes) = await GetVoteTallyAsync(watch.Id, ct);
        }

        return new RatWatchDto
        {
            Id = watch.Id,
            GuildId = watch.GuildId,
            GuildName = guildName,
            ChannelId = watch.ChannelId,
            AccusedUserId = watch.AccusedUserId,
            AccusedUsername = accusedUsername,
            InitiatorUserId = watch.InitiatorUserId,
            InitiatorUsername = initiatorUsername,
            OriginalMessageId = watch.OriginalMessageId,
            CustomMessage = watch.CustomMessage,
            ScheduledAt = watch.ScheduledAt,
            CreatedAt = watch.CreatedAt,
            Status = watch.Status,
            VotingMessageId = watch.VotingMessageId,
            VotingStartedAt = watch.VotingStartedAt,
            VotingEndedAt = watch.VotingEndedAt,
            ClearedAt = watch.ClearedAt,
            GuiltyVotes = guiltyVotes,
            NotGuiltyVotes = notGuiltyVotes
        };
    }

    /// <summary>
    /// Gets the guild name from Discord or falls back to the database name.
    /// Returns "Unknown Guild" if neither is available.
    /// </summary>
    private string GetGuildName(ulong guildId, string? databaseName)
    {
        var guild = _client.GetGuild(guildId);
        if (guild != null)
        {
            return guild.Name;
        }

        if (!string.IsNullOrEmpty(databaseName))
        {
            return databaseName;
        }

        _logger.LogDebug("Guild {GuildId} not found in Discord client or database", guildId);
        return "Unknown Guild";
    }

    /// <inheritdoc/>
    public async Task<bool> HasActiveWatchesAsync(CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "has_active_watches");

        try
        {
            _logger.LogTrace("Checking for any active Rat Watches");

            var result = await _watchRepository.HasActiveWatchesAsync(ct);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<RatWatchDto> Items, int TotalCount)> GetFilteredByGuildAsync(
        ulong guildId,
        RatWatchIncidentFilterDto filter,
        CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_filtered_by_guild",
            guildId: guildId);

        try
        {
            _logger.LogDebug(
                "Getting filtered Rat Watches for guild {GuildId} with filters: Statuses={Statuses}, StartDate={StartDate}, EndDate={EndDate}, AccusedUser={AccusedUser}, InitiatorUser={InitiatorUser}, MinVoteCount={MinVoteCount}, Keyword={Keyword}, Page={Page}, PageSize={PageSize}",
                guildId,
                filter.Statuses != null ? string.Join(",", filter.Statuses) : "null",
                filter.StartDate,
                filter.EndDate,
                filter.AccusedUser,
                filter.InitiatorUser,
                filter.MinVoteCount,
                filter.Keyword,
                filter.Page,
                filter.PageSize);

            var (watches, totalCount) = await _watchRepository.GetFilteredByGuildAsync(guildId, filter, ct);
            var dtos = new List<RatWatchDto>();

            foreach (var watch in watches)
            {
                dtos.Add(await MapToDtoAsync(watch, ct));
            }

            _logger.LogInformation(
                "Retrieved {Count} of {Total} filtered Rat Watches for guild {GuildId}",
                dtos.Count, totalCount, guildId);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return (dtos, totalCount);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<RatWatchDto>> GetRecentActivityAsync(int limit = 10, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "get_recent_activity");

        try
        {
            _logger.LogDebug("Getting {Limit} most recent Rat Watch events", limit);

            var watches = await _watchRepository.GetRecentAsync(limit, ct);
            var dtos = new List<RatWatchDto>();

            foreach (var watch in watches)
            {
                dtos.Add(await MapToDtoAsync(watch, ct));
            }

            _logger.LogInformation("Retrieved {Count} recent Rat Watch events", dtos.Count);

            BotActivitySource.SetRecordsReturned(activity, dtos.Count);
            BotActivitySource.SetSuccess(activity);
            return dtos;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasStatusAsync(Guid watchId, RatWatchStatus expectedStatus, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "rat_watch",
            "has_status",
            entityId: watchId.ToString());

        try
        {
            _logger.LogDebug("Checking if Rat Watch {WatchId} has status {ExpectedStatus}", watchId, expectedStatus);

            var result = await _watchRepository.HasStatusAsync(watchId, expectedStatus, ct);

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the username for a Discord user.
    /// Returns "Unknown User" if the user cannot be found.
    /// </summary>
    private async Task<string> GetUsernameAsync(ulong userId, ulong guildId)
    {
        try
        {
            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found when resolving username for user {UserId}", guildId, userId);
                return "Unknown User";
            }

            var user = guild.GetUser(userId);
            if (user != null)
            {
                return user.DisplayName;
            }

            // Try downloading users if not in cache
            // SocketGuild doesn't have GetUserAsync, so we download all users and check again
            if (!guild.HasAllMembers)
            {
                await guild.DownloadUsersAsync();
                user = guild.GetUser(userId);
                if (user != null)
                {
                    return user.DisplayName;
                }
            }

            _logger.LogDebug("User {UserId} not found in guild {GuildId}", userId, guildId);
            return "Unknown User";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get username for user {UserId} in guild {GuildId}", userId, guildId);
            return "Unknown User";
        }
    }
}
